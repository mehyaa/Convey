using System;

namespace Convey.MessageBrokers.RabbitMQ;

public interface IConventionProvider
{
    IConvention Get<T>();
    IConvention Get(Type type);
}