using System;

namespace Convey.MessageBrokers.RabbitMQ;

public interface IConventionProvider
{
    IConvention Get(Type type);
}