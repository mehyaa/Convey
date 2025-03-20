using Convey.MessageBrokers.RabbitMQ.Plugins;
using Convey.MessageBrokers.RabbitMQ.Subscribers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Convey.MessageBrokers.RabbitMQ.Internals;

internal sealed class RabbitMqBackgroundService : BackgroundService, IAsyncDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        WriteIndented = true
    };

    private static readonly EmptyExceptionToMessageMapper ExceptionToMessageMapper = new();
    private static readonly EmptyExceptionToFailedMessageMapper ExceptionToFailedMessageMapper = new();

    private readonly ConcurrentDictionary<string, IChannel> _channels = new();

    private readonly IServiceProvider _serviceProvider;

    private readonly IConnection _consumerConnection;
    private readonly IConnection _producerConnection;
    private readonly MessageSubscribersChannel _messageSubscribersChannel;
    private readonly IBusPublisher _publisher;
    private readonly IRabbitMqSerializer _rabbitMqSerializer;
    private readonly IConventionProvider _conventionsProvider;
    private readonly IContextProvider _contextProvider;
    private readonly IRabbitMqPluginsExecutor _pluginsExecutor;
    private readonly IExceptionToMessageMapper _exceptionToMessageMapper;
    private readonly IExceptionToFailedMessageMapper _exceptionToFailedMessageMapper;
    private readonly RabbitMqOptions _options;
    private readonly ILogger _logger;

    private readonly bool _loggerEnabled;
    private readonly bool _logMessagePayload;
    private readonly int _retries;
    private readonly int _retryInterval;
    private readonly RabbitMqOptions.QosOptions _qosOptions;
    private readonly bool _requeueFailedMessages;

    public RabbitMqBackgroundService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;

        _consumerConnection = _serviceProvider.GetRequiredService<ConsumerConnection>().Connection;
        _producerConnection = _serviceProvider.GetRequiredService<ProducerConnection>().Connection;
        _messageSubscribersChannel = _serviceProvider.GetRequiredService<MessageSubscribersChannel>();
        _publisher = _serviceProvider.GetRequiredService<IBusPublisher>();
        _rabbitMqSerializer = _serviceProvider.GetRequiredService<IRabbitMqSerializer>();
        _conventionsProvider = _serviceProvider.GetRequiredService<IConventionProvider>();
        _contextProvider = _serviceProvider.GetRequiredService<IContextProvider>();
        _pluginsExecutor = _serviceProvider.GetRequiredService<IRabbitMqPluginsExecutor>();
        _exceptionToMessageMapper = _serviceProvider.GetService<IExceptionToMessageMapper>() ?? ExceptionToMessageMapper;
        _exceptionToFailedMessageMapper = _serviceProvider.GetService<IExceptionToFailedMessageMapper>() ?? ExceptionToFailedMessageMapper;
        _options = _serviceProvider.GetRequiredService<RabbitMqOptions>();
        _logger = _serviceProvider.GetRequiredService<ILogger<RabbitMqSubscriber>>();

        _loggerEnabled = _options.Logger?.Enabled ?? false;
        _logMessagePayload = _options.Logger?.LogMessagePayload ?? false;
        _retries = _options.Retries >= 0 ? _options.Retries : 3;
        _retryInterval = _options.RetryInterval > 0 ? _options.RetryInterval : 2;
        _qosOptions = _options.Qos ?? new RabbitMqOptions.QosOptions();
        _requeueFailedMessages = _options.RequeueFailedMessages;

        if (_qosOptions.PrefetchCount < 1)
        {
            _qosOptions.PrefetchCount = 1;
        }

        if (!_loggerEnabled || _options.Logger?.LogConnectionStatus is not true)
        {
            return;
        }

        _consumerConnection.CallbackExceptionAsync += ConnectionOnCallbackExceptionAsync;
        _consumerConnection.ConnectionShutdownAsync += ConnectionOnConnectionShutdownAsync;
        _consumerConnection.ConnectionBlockedAsync += ConnectionOnConnectionBlockedAsync;
        _consumerConnection.ConnectionUnblockedAsync += ConnectionOnConnectionUnblockedAsync;

        _producerConnection.CallbackExceptionAsync += ConnectionOnCallbackExceptionAsync;
        _producerConnection.ConnectionShutdownAsync += ConnectionOnConnectionShutdownAsync;
        _producerConnection.ConnectionBlockedAsync += ConnectionOnConnectionBlockedAsync;
        _producerConnection.ConnectionUnblockedAsync += ConnectionOnConnectionUnblockedAsync;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var messageSubscriber in _messageSubscribersChannel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                switch (messageSubscriber.Action)
                {
                    case MessageSubscriberAction.Subscribe:
                        await SubscribeAsync(messageSubscriber);
                        break;

                    case MessageSubscriberAction.Unsubscribe:
                        Unsubscribe(messageSubscriber);
                        break;

                    default:
                        throw new InvalidOperationException("Unknown message subscriber action type.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "There was an error during RabbitMQ action: '{Action}'", messageSubscriber.Action);
            }
        }
    }

    private async Task SubscribeAsync(IMessageSubscriber messageSubscriber)
    {
        var convention = _conventionsProvider.Get(messageSubscriber.Type);
        var channelKey = GetChannelKey(convention);

        if (_channels.ContainsKey(channelKey))
        {
            return;
        }

        var channel = await _consumerConnection.CreateChannelAsync();

        if (!_channels.TryAdd(channelKey, channel))
        {
            _logger.LogError(
                "Couldn't add the channel for exchange: '{Exchange}', queue: '{Queue}', routing key: '{RoutingKey}'",
                convention.Exchange,
                convention.Queue,
                convention.RoutingKey);

            channel.Dispose();

            return;
        }

        _logger.LogTrace(
            "Added the channel: {ChannelNumber} for exchange: '{Exchange}', queue: '{Queue}', routing key: '{RoutingKey}'",
            channel.ChannelNumber,
            convention.Exchange,
            convention.Queue,
            convention.RoutingKey);

        var declare = _options.Queue?.Declare ?? true;
        var durable = _options.Queue?.Durable ?? true;
        var exclusive = _options.Queue?.Exclusive ?? false;
        var autoDelete = _options.Queue?.AutoDelete ?? false;

        var deadLetterEnabled = _options.DeadLetter?.Enabled is true;

        var deadLetterExchange =
            deadLetterEnabled
                ? $"{_options.DeadLetter.Prefix}{_options.Exchange.Name}{_options.DeadLetter.Suffix}"
                : string.Empty;

        var deadLetterQueue =
            deadLetterEnabled
                ? $"{_options.DeadLetter.Prefix}{convention.Queue}{_options.DeadLetter.Suffix}"
                : string.Empty;

        if (declare)
        {
            if (_loggerEnabled)
            {
                _logger.LogInformation(
                    "Declaring a queue: '{Queue}' with routing key: '{RoutingKey}' for an exchange: '{Exchange}'",
                    convention.Exchange,
                    convention.Queue,
                    convention.RoutingKey);
            }

            var queueArguments =
                deadLetterEnabled
                    ? new Dictionary<string, object>
                    {
                        {"x-dead-letter-exchange", deadLetterExchange},
                        {"x-dead-letter-routing-key", deadLetterQueue},
                    }
                    : [];

            await channel.QueueDeclareAsync(convention.Queue, durable, exclusive, autoDelete, queueArguments);
        }

        await channel.QueueBindAsync(convention.Queue, convention.Exchange, convention.RoutingKey);
        await channel.BasicQosAsync(_qosOptions.PrefetchSize, _qosOptions.PrefetchCount, _qosOptions.Global);

        if (_options.DeadLetter?.Enabled is true)
        {
            if (_options.DeadLetter.Declare)
            {
                var ttl =
                    _options.DeadLetter.Ttl.HasValue
                        ? _options.DeadLetter.Ttl <= 0 ? 86400000 : _options.DeadLetter.Ttl
                        : null;

                var deadLetterArgs =
                    new Dictionary<string, object>
                    {
                        { "x-dead-letter-exchange", convention.Exchange },
                        { "x-dead-letter-routing-key", convention.Queue }
                    };

                if (ttl.HasValue)
                {
                    deadLetterArgs["x-message-ttl"] = ttl.Value;
                }

                _logger.LogInformation(
                    "Declaring a dead letter queue: '{Queue}' for an exchange: '{Exchange}', message TTL: {TTL} ms",
                    deadLetterQueue,
                    deadLetterExchange,
                    ttl);

                await channel.QueueDeclareAsync(
                    deadLetterQueue,
                    _options.DeadLetter.Durable,
                    _options.DeadLetter.Exclusive,
                    _options.DeadLetter.AutoDelete,
                    deadLetterArgs);
            }

            await channel.QueueBindAsync(deadLetterQueue, deadLetterExchange, deadLetterQueue);
        }

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (_, args) =>
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();

                var scopedServiceProvider = scope.ServiceProvider;

                var messageId = args.BasicProperties.MessageId;
                var correlationId = args.BasicProperties.CorrelationId;
                var timestamp = args.BasicProperties.Timestamp.UnixTime;
                var message = _rabbitMqSerializer.Deserialize(args.Body.Span, messageSubscriber.Type);

                if (_loggerEnabled)
                {
                    var messagePayload = _logMessagePayload ? Encoding.UTF8.GetString(args.Body.Span) : string.Empty;

                    _logger.LogInformation(
                        "Received a message with ID: '{MessageId}', " +
                        "Correlation ID: '{CorrelationId}', timestamp: {Timestamp}, " +
                        "queue: {Queue}, routing key: {RoutingKey}, exchange: {Exchange}, payload: {MessagePayload}",
                        messageId,
                        correlationId,
                        timestamp,
                        convention.Queue,
                        convention.RoutingKey,
                        convention.Exchange,
                        messagePayload);
                }

                var correlationContext = BuildCorrelationContext(scopedServiceProvider, args, message, messageSubscriber.Type);

                Task Next(object m, object ctx, BasicDeliverEventArgs a)
                    => TryHandleAsync(channel, m, messageId, correlationId, ctx, a, scopedServiceProvider, messageSubscriber.Handle, deadLetterEnabled);

                await _pluginsExecutor.ExecuteAsync(Next, message, correlationContext, args);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                await channel.BasicNackAsync(args.DeliveryTag, false, _requeueFailedMessages);
                await Task.Yield();
            }
        };

        await channel.BasicConsumeAsync(convention.Queue, false, consumer);
    }

    private object BuildCorrelationContext(IServiceProvider serviceProvider, BasicDeliverEventArgs args, object message, Type messageType)
    {
        var messagePropertiesAccessor = serviceProvider.GetRequiredService<IMessagePropertiesAccessor>();

        var messageProperties = new MessageProperties
        {
            MessageId = args.BasicProperties.MessageId,
            CorrelationId = args.BasicProperties.CorrelationId,
            Timestamp = args.BasicProperties.Timestamp.UnixTime,
            Headers = args.BasicProperties.Headers
        };

        messagePropertiesAccessor.MessageProperties = messageProperties;

        var correlationContextAccessor = serviceProvider.GetRequiredService<ICorrelationContextAccessor>();
        var correlationContext = _contextProvider.Get(message, messageType, messageProperties);

        correlationContextAccessor.CorrelationContext = correlationContext;

        return correlationContext;
    }

    private async Task TryHandleAsync(
        IChannel channel,
        object message,
        string messageId,
        string correlationId,
        object messageContext,
        BasicDeliverEventArgs args,
        IServiceProvider serviceProvider,
        Func<IServiceProvider, object, object, Task> handle,
        bool deadLetterEnabled)
    {
        var currentRetry = 0;
        var messageName = message.GetType().Name.Underscore();

        var retryPolicy =
            Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(_retries, _ => TimeSpan.FromSeconds(_retryInterval));

        await retryPolicy.ExecuteAsync(async () =>
        {
            try
            {
                if (_loggerEnabled)
                {
                    _logger.LogInformation(
                        "Handling a message: {MessageName} with ID: {MessageId}, Correlation ID: {CorrelationId}, retry: {MessageRetry}",
                        messageName,
                        messageId,
                        correlationId,
                        currentRetry);
                }

                if (_options.MessageProcessingTimeout.HasValue)
                {
                    var task = handle(serviceProvider, message, messageContext);
                    var result = await Task.WhenAny(task, Task.Delay(_options.MessageProcessingTimeout.Value));

                    if (result != task)
                    {
                        throw new RabbitMqMessageProcessingTimeoutException(messageId, correlationId);
                    }
                }
                else
                {
                    await handle(serviceProvider, message, messageContext);
                }

                await channel.BasicAckAsync(args.DeliveryTag, false);

                await Task.Yield();

                if (_loggerEnabled)
                {
                    _logger.LogInformation(
                        "Handled a message: {MessageName} with ID: {MessageId}, Correlation ID: {CorrelationId}, retry: {MessageRetry}",
                        messageName,
                        messageId,
                        correlationId,
                        currentRetry);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);

                if (ex is RabbitMqMessageProcessingTimeoutException)
                {
                    await channel.BasicNackAsync(args.DeliveryTag, false, _requeueFailedMessages);

                    await Task.Yield();

                    return;
                }

                currentRetry++;

                var hasNextRetry = currentRetry <= _retries;

                var failedMessage = _exceptionToFailedMessageMapper.Map(ex, message);
                if (failedMessage is null)
                {
                    // This is a fallback to the previous mechanism in order to avoid the legacy related issues
                    var rejectedEvent = _exceptionToMessageMapper.Map(ex, message);
                    if (rejectedEvent is not null)
                    {
                        failedMessage = new FailedMessage(rejectedEvent, false);
                    }
                }

                if (failedMessage?.Message is not null && (!failedMessage.ShouldRetry || !hasNextRetry))
                {
                    var failedMessageName = failedMessage.Message.GetType().Name.Underscore();
                    var failedMessageId = Guid.NewGuid().ToString("N");

                    await _publisher.PublishAsync(failedMessage.Message, failedMessageId, correlationId, messageContext: messageContext);

                    _logger.LogError(ex, ex.Message);

                    if (_loggerEnabled)
                    {
                        _logger.LogWarning(
                            "Published a failed messaged: {FailedMessageName} with ID: {FailedMessageId}, " +
                            "Correlation ID: {CorrelationId}, for the message: {MessageName} with ID: {MessageId}",
                            failedMessageName,
                            failedMessageId,
                            correlationId,
                            messageName,
                            messageId);
                    }

                    if (!deadLetterEnabled || !failedMessage.MoveToDeadLetter)
                    {
                        await channel.BasicAckAsync(args.DeliveryTag, false);

                        await Task.Yield();

                        return;
                    }
                }

                if (failedMessage is null || failedMessage.ShouldRetry)
                {
                    var errorMessage =
                        $"Unable to handle a message: '{messageName}' with ID: '{messageId}', " +
                        $"Correlation ID: '{correlationId}', retry {currentRetry}/{_retries}...";

                    _logger.LogError(errorMessage);

                    if (hasNextRetry)
                    {
                        throw new Exception(errorMessage, ex);
                    }
                }

                _logger.LogError(
                    "Handling a message: {MessageName} with ID: {MessageId}, Correlation ID: {CorrelationId} failed",
                    messageName,
                    messageId,
                    correlationId);

                if (failedMessage is not null && !failedMessage.MoveToDeadLetter)
                {
                    await channel.BasicAckAsync(args.DeliveryTag, false);

                    await Task.Yield();

                    return;
                }

                if (deadLetterEnabled)
                {
                    _logger.LogError(
                        "Message: {MessageName} with ID: {MessageId}, Correlation ID: {CorrelationId} will be moved to DLX",
                        messageName,
                        messageId,
                        correlationId);
                }

                await channel.BasicNackAsync(args.DeliveryTag, false, _requeueFailedMessages);

                await Task.Yield();
            }
        });
    }

    private void Unsubscribe(IMessageSubscriber messageSubscriber)
    {
        var type = messageSubscriber.Type;
        var convention = _conventionsProvider.Get(type);
        var channelKey = GetChannelKey(convention);

        if (!_channels.TryRemove(channelKey, out var channel))
        {
            return;
        }

        channel.Dispose();

        _logger.LogTrace(
            "Removed channel: {ChannelNumber}, exchange: '{Exchange}', queue: '{Queue}', routing key: '{RoutingKey}'",
            channel.ChannelNumber,
            convention.Exchange,
            convention.Queue,
            convention.RoutingKey);
    }

    private static string GetChannelKey(IConvention convention)
        => $"{convention.Exchange}:{convention.RoutingKey}:{convention.Queue}";

    public async ValueTask DisposeAsync()
    {
        if (_loggerEnabled && _options.Logger?.LogConnectionStatus is true)
        {
            _consumerConnection.CallbackExceptionAsync -= ConnectionOnCallbackExceptionAsync;
            _consumerConnection.ConnectionShutdownAsync -= ConnectionOnConnectionShutdownAsync;
            _consumerConnection.ConnectionBlockedAsync -= ConnectionOnConnectionBlockedAsync;
            _consumerConnection.ConnectionUnblockedAsync -= ConnectionOnConnectionUnblockedAsync;

            _producerConnection.CallbackExceptionAsync -= ConnectionOnCallbackExceptionAsync;
            _producerConnection.ConnectionShutdownAsync -= ConnectionOnConnectionShutdownAsync;
            _producerConnection.ConnectionBlockedAsync -= ConnectionOnConnectionBlockedAsync;
            _producerConnection.ConnectionUnblockedAsync -= ConnectionOnConnectionUnblockedAsync;
        }

        foreach (var (key, channel) in _channels)
        {
            channel?.Dispose();
            _channels.TryRemove(key, out _);
        }

        try
        {
            await _consumerConnection.CloseAsync();
            await _producerConnection.CloseAsync();
        }
        catch
        {
            // ignored
        }
    }

    private class EmptyExceptionToMessageMapper : IExceptionToMessageMapper
    {
        public object Map(Exception exception, object message) => null;
    }

    private class EmptyExceptionToFailedMessageMapper : IExceptionToFailedMessageMapper
    {
        public FailedMessage Map(Exception exception, object message) => null;
    }

    private Task ConnectionOnCallbackExceptionAsync(object sender, CallbackExceptionEventArgs eventArgs)
    {
        var details =
            eventArgs.Detail is not null
                ? JsonSerializer.Serialize(eventArgs.Detail, SerializerOptions)
                : string.Empty;

        if (eventArgs.Exception is not null)
        {
            _logger.LogError(eventArgs.Exception, "RabbitMQ callback exception occured, details: {Details}", details);
        }
        else
        {
            _logger.LogError("RabbitMQ callback exception occured, details: {Details}", details);
        }
        
        return Task.CompletedTask;
    }

    private Task ConnectionOnConnectionShutdownAsync(object sender, ShutdownEventArgs eventArgs)
    {
        _logger.LogError(
            "RabbitMQ connection shutdown occured. Initiator: '{Initiator}', reply code: '{ReplyCode}', text: '{ReplyText}'",
            eventArgs.Initiator,
            eventArgs.ReplyCode,
            eventArgs.ReplyText);
        
        return Task.CompletedTask;
    }

    private Task ConnectionOnConnectionBlockedAsync(object sender, ConnectionBlockedEventArgs eventArgs)
    {
        _logger.LogError("RabbitMQ connection has been blocked: {Reason}", eventArgs.Reason);
        
        return Task.CompletedTask;
    }

    private Task ConnectionOnConnectionUnblockedAsync(object sender, AsyncEventArgs eventArgs)
    {
        _logger.LogInformation("RabbitMQ connection has been unblocked");
        
        return Task.CompletedTask;
    }
}