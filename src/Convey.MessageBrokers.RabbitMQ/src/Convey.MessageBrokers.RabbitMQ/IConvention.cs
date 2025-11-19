using System;

namespace Convey.MessageBrokers.RabbitMQ;

public interface IConvention
{
    Type Type { get; }
    string Exchange { get; }
    string RoutingKey { get; }
    string Queue { get; }
}