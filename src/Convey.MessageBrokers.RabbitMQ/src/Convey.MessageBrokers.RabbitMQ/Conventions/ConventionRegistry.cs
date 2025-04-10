using System;
using System.Collections.Generic;

namespace Convey.MessageBrokers.RabbitMQ.Conventions;

public class ConventionRegistry : IConventionRegistry
{
    private readonly IDictionary<Type, IConvention> _conventions = new Dictionary<Type, IConvention>();

    public void Add<T>(IConvention convention) => Add(typeof(T), convention);

    public void Add(Type type, IConvention convention) => _conventions[type] = convention;

    public IConvention Get<T>() => Get(typeof(T));

    public IConvention Get(Type type) => _conventions.TryGetValue(type, out var convention) ? convention : null;

    public IEnumerable<IConvention> GetAll() => _conventions.Values;
}