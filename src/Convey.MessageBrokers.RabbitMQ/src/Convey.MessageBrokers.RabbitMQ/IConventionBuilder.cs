using System;

namespace Convey.MessageBrokers.RabbitMQ;

public interface IConventionBuilder
{
    string GetRoutingKey(Type type);
    string GetExchange(Type type);
    string GetQueue(Type type);
}