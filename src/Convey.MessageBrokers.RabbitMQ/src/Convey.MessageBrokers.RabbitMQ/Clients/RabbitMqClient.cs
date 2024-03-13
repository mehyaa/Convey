using Convey.Types;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Convey.MessageBrokers.RabbitMQ.Clients;

internal sealed class RabbitMqClient : IRabbitMqClient
{
    private const string EmptyContext = "{}";

    private readonly object _lockObject = new();

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

    private readonly ConcurrentDictionary<int, IModel> _channels = new();

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
    }

    public void Send(
        object message,
        IConvention convention,
        string messageId = null,
        string correlationId = null,
        string spanContext = null,
        object messageContext = null,
        IDictionary<string, object> headers = null)
    {
        var threadId = Environment.CurrentManagedThreadId;

        if (!_channels.TryGetValue(threadId, out var channel))
        {
            lock (_lockObject)
            {
                if (_channelsCount >= _maxChannels)
                {
                    throw new InvalidOperationException(
                        $"Cannot create RabbitMQ producer channel for thread: {threadId} " +
                        $"(reached the limit of {_maxChannels} channels). " +
                        "Modify `MaxProducerChannels` setting to allow more channels.");
                }

                channel = _connection.CreateModel();
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

        var properties = channel.CreateBasicProperties();

        properties.AppId = _appOptions.Service;
        properties.ContentEncoding = _serializer.ContentEncoding;
        properties.ContentType = _serializer.ContentType;
        properties.Persistent = _persistMessages;
        properties.MessageId = string.IsNullOrWhiteSpace(messageId) ? Guid.NewGuid().ToString("N") : messageId;
        properties.CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString("N") : correlationId;
        properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        properties.Type = convention.Type?.Name;
        properties.Headers = new Dictionary<string, object>();

        if (_contextEnabled)
        {
            IncludeMessageContext(messageContext, properties);
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

        var body = _serializer.Serialize(message);

        channel.BasicPublish(convention.Exchange, convention.RoutingKey, properties, body.ToArray());
    }

    private void IncludeMessageContext(object context, IBasicProperties properties)
    {
        if (context is not null)
        {
            properties.Headers.Add(_contextProvider.HeaderName, _serializer.Serialize(context).ToArray());

            return;
        }

        properties.Headers.Add(_contextProvider.HeaderName, EmptyContext);
    }
}