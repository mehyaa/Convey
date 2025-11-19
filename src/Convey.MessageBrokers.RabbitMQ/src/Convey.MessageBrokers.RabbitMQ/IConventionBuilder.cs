using System;

namespace Convey.MessageBrokers.RabbitMQ;

public interface IConventionBuilder
{
    string GetExchange(Type type);
    string GetRoutingKey(Type type);
    string GetQueue(Type type);
}