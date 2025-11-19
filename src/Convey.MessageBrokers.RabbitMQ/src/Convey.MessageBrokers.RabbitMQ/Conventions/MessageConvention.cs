using System;

namespace Convey.MessageBrokers.RabbitMQ.Conventions;

public class MessageConvention : IConvention
{
    public Type Type { get; }
    public string Exchange { get; }
    public string RoutingKey { get; }
    public string Queue { get; }

    public MessageConvention(Type type, string exchange, string routingKey, string queue)
    {
        Type = type;
        Exchange = exchange;
        RoutingKey = routingKey;
        Queue = queue;
    }
}