using Convey.Types;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Convey.MessageBrokers.RabbitMQ.Clients;

internal sealed class RabbitMqClient : IRabbitMqClient
{
    private const string EmptyContext = "{}";

    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private readonly AppOptions _appOptions;
    private readonly IConnection _connection;
    private readonly IContextProvider _contextProvider;
    private readonly IRabbitMqSerializer _serializer;
    private readonly ILogger<RabbitMqClient> _logger;
    private readonly bool _contextEnabled;
    private readonly bool _loggerEnabled;
    private readonly string _spanContextHeader;
    private readonly bool _persistMessages;
    private readonly int _maxChannels;
    private readonly string _contentType;
    private readonly string _contentEncoding;

    private readonly ConcurrentDictionary<int, IChannel> _channels = new();

    private int _channelsCount;

    public RabbitMqClient(
        AppOptions appOptions,
        ProducerConnection connection,
        IContextProvider contextProvider,
        IRabbitMqSerializer serializer,
        RabbitMqOptions options,
        ILogger<RabbitMqClient> logger)
    {
        _connection = connection.Connection;
        _appOptions = appOptions;
        _contextProvider = contextProvider;
        _serializer = serializer;
        _logger = logger;
        _contextEnabled = options.Context?.Enabled == true;
        _loggerEnabled = options.Logger?.Enabled ?? false;
        _spanContextHeader = options.GetSpanContextHeader();
        _persistMessages = options?.MessagesPersisted ?? false;
        _maxChannels = options.MaxProducerChannels <= 0 ? 1000 : options.MaxProducerChannels;
        _contentType = options.Publish?.ContentType ?? "application/json";
        _contentEncoding = options.Publish?.ContentEncoding ?? "UTF-8";
    }

    public async Task SendAsync(
        object message,
        IConvention convention,
        string messageId = null,
        string correlationId = null,
        string spanContext = null,
        object messageContext = null,
        IDictionary<string, object> headers = null,
        CancellationToken cancellationToken = default)
    {
        var threadId = Environment.CurrentManagedThreadId;

        if (!_channels.TryGetValue(threadId, out var channel))
        {
            try
            {
                await _semaphore.WaitAsync(cancellationToken);

                if (_channelsCount >= _maxChannels)
                {
                    throw new InvalidOperationException(
                        $"Cannot create RabbitMQ producer channel for thread: {threadId} " +
                        $"(reached the limit of {_maxChannels} channels). " +
                        "Modify `MaxProducerChannels` setting to allow more channels.");
                }

                channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
                _channels.TryAdd(threadId, channel);
                _channelsCount++;

                if (_loggerEnabled)
                {
                    _logger.LogTrace(
                        "Created a channel for thread: {ThreadId}, total channels: {ChannelsCount}/{MaxChannels}",
                        threadId,
                        _channelsCount,
                        _maxChannels);
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }
        else
        {
            if (_loggerEnabled)
            {
                _logger.LogTrace(
                    "Reused a channel for thread: {ThreadId}, total channels: {ChannelsCount}/{MaxChannels}",
                    threadId,
                    _channelsCount,
                    _maxChannels);
            }
        }

        var properties = new BasicProperties
        {
            AppId = _appOptions.Service,
            ContentEncoding = _contentEncoding,
            ContentType = _contentType,
            Persistent = _persistMessages,
            MessageId = string.IsNullOrWhiteSpace(messageId) ? Guid.NewGuid().ToString("N") : messageId,
            CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString("N") : correlationId,
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            Type = convention.Type?.Name,
            Headers = new Dictionary<string, object>()
        };

        if (_contextEnabled)
        {
            await IncludeMessageContextAsync(messageContext, properties, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(spanContext))
        {
            properties.Headers.Add(_spanContextHeader, spanContext);
        }

        if (headers is not null)
        {
            foreach (var (key, value) in headers)
            {
                if (string.IsNullOrWhiteSpace(key) || value is null)
                {
                    continue;
                }

                properties.Headers.TryAdd(key, value);
            }
        }

        if (_loggerEnabled)
        {
            _logger.LogTrace(
                "Publishing a message with routing key: '{RoutingKey}' to exchange: '{Exchange}' [id: '{MessageId}', correlation id: '{CorrelationId}']",
                convention.RoutingKey,
                convention.Exchange,
                properties.MessageId,
                properties.CorrelationId);
        }

        var body = await _serializer.SerializeAsync(message, _contentType, cancellationToken);

        await channel.BasicPublishAsync(
            exchange: convention.Exchange,
            routingKey: convention.RoutingKey,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);
    }

    private async Task IncludeMessageContextAsync(object context, BasicProperties properties, CancellationToken cancellationToken)
    {
        if (properties?.Headers is null)
        {
            return;
        }

        if (context is not null)
        {
            properties.Headers.Add(_contextProvider.HeaderName, await _serializer.SerializeAsync(context, _contentType, cancellationToken));

            return;
        }

        properties.Headers.Add(_contextProvider.HeaderName, EmptyContext);
    }
}