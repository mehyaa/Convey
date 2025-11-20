using System;
using System.Threading;
using System.Threading.Tasks;

namespace Convey.MessageBrokers.RabbitMQ;

public interface IRabbitMqSerializer
{
    Task<byte[]> SerializeAsync<T>(T value, string contentType, CancellationToken cancellationToken = default)
        where T : class;

    Task<object> DeserializeAsync(byte[] value, Type type, string contentType, CancellationToken cancellationToken = default);

    async Task<T> DeserializeAsync<T>(byte[] value, string contentType, CancellationToken cancellationToken = default)
        where T : class
        => (T)await DeserializeAsync(value, typeof(T), contentType, cancellationToken);
}