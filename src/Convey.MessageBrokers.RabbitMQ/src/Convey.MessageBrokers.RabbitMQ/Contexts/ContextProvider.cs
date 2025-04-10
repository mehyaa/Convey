using System;
using System.Threading;
using System.Threading.Tasks;

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

    public async Task<object> GetAsync(
        object message,
        Type messageType,
        MessageProperties messageProperties,
        string contentType,
        CancellationToken cancellationToken = default)
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
            return await _serializer.DeserializeAsync<object>(bytes, contentType, cancellationToken);
        }

        return null;
    }
}