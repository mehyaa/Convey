using System;

namespace Convey.MessageBrokers.RabbitMQ;

public interface IContextProvider
{
    string HeaderName { get; }
    object Get(object message, Type messageType, MessageProperties messageProperties);
}