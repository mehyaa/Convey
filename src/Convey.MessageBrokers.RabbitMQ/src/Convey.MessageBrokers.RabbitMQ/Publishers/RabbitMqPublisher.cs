using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Convey.MessageBrokers.RabbitMQ.Publishers;

internal sealed class RabbitMqPublisher : IBusPublisher
{
    private readonly IRabbitMqClient _client;
    private readonly IConventionProvider _conventionsProvider;

    public RabbitMqPublisher(IRabbitMqClient client, IConventionProvider conventionsProvider)
    {
        _client = client;
        _conventionsProvider = conventionsProvider;
    }

    public Task PublishAsync<T>(
        T message,
        string messageId = null,
        string correlationId = null,
        string spanContext = null,
        object messageContext = null,
        IDictionary<string, object> headers = null,
        CancellationToken cancellationToken = default)
        where T : class
        => _client.SendAsync(
            message,
            _conventionsProvider.Get<T>(),
            messageId,
            correlationId,
            spanContext,
            messageContext,
            headers,
            cancellationToken);
}