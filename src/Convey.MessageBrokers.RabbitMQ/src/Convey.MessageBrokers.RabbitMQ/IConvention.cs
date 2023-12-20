using System;

namespace Convey.MessageBrokers.RabbitMQ;

public interface IConvention
{
    Type Type { get; }
    string RoutingKey { get; }
    string Exchange { get; }
    string Queue { get; }
}