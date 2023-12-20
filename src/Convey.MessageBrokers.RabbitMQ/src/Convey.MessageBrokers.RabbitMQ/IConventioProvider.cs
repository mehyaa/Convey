using System;

namespace Convey.MessageBrokers.RabbitMQ;

public interface IConventioProvider
{
    IConvention Get<T>();
    IConvention Get(Type type);
}