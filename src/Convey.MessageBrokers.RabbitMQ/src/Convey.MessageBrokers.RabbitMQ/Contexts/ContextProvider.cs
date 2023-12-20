using System;

namespace Convey.MessageBrokers.RabbitMQ.Contexts;

internal sealed class ContextProvider : IContextProvider
{
    private readonly IRabbitMqSerializer _serializer;

    public string HeaderName { get; }

    public ContextProvider(IRabbitMqSerializer serializer, RabbitMqOptions options)
    {
        _serializer = serializer;

        HeaderName =
            string.IsNullOrWhiteSpace(options.Context?.Header)
                ? "message_context"
                : options.Context.Header;
    }

    public object Get(object message, Type messageType, MessageProperties messageProperties)
    {
        if (messageProperties.Headers is null)
        {
            return null;
        }

        if (!messageProperties.Headers.TryGetValue(HeaderName, out var context))
        {
            return null;
        }

        if (context is byte[] bytes)
        {
            return _serializer.Deserialize(bytes);
        }

        return null;
    }
}