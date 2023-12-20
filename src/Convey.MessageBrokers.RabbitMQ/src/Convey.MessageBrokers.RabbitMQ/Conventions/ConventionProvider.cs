using System;
using System.Collections.Concurrent;

namespace Convey.MessageBrokers.RabbitMQ.Conventions;

public class ConventionProvider : IConventioProvider
{
    private readonly ConcurrentDictionary<Type, IConvention> _conventions = new();

    private readonly IConventionRegistry _registry;
    private readonly IConventionBuilder _builder;

    public ConventionProvider(IConventionRegistry registry, IConventionBuilder builder)
    {
        _registry = registry;
        _builder = builder;
    }

    public IConvention Get<T>() => Get(typeof(T));

    public IConvention Get(Type type)
    {
        if (_conventions.TryGetValue(type, out var convention))
        {
            return convention;
        }

        convention =
            _registry.Get(type) ??
            new MessageConvention(
                type,
                _builder.GetRoutingKey(type),
                _builder.GetExchange(type),
                _builder.GetQueue(type));

        _conventions.TryAdd(type, convention);

        return convention;
    }
}