using System;
using System.Collections.Generic;

namespace Convey.MessageBrokers.RabbitMQ;

public interface IConventionRegistry
{
    void Add<T>(IConvention convention);
    void Add(Type type, IConvention convention);
    IConvention Get<T>();
    IConvention Get(Type type);
    IEnumerable<IConvention> GetAll();
}