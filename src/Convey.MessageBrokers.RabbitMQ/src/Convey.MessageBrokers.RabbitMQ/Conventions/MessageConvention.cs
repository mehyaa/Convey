using System;

namespace Convey.MessageBrokers.RabbitMQ.Conventions;

public class MessageConvention : IConvention
{
    public Type Type { get; }
    public string RoutingKey { get; }
    public string Exchange { get; }
    public string Queue { get; }

    public MessageConvention(Type type, string routingKey, string exchange, string queue)
    {
        Type = type;
        RoutingKey = routingKey;
        Exchange = exchange;
        Queue = queue;
    }
}