using System;
using System.Threading;
using System.Threading.Tasks;

namespace Convey.MessageBrokers.RabbitMQ;

public interface IContextProvider
{
    string HeaderName { get; }
    Task<object> GetAsync(object message, Type messageType, MessageProperties messageProperties, string contentType, CancellationToken cancellationToken = default);
}