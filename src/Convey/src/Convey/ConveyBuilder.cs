using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Convey;

public sealed class ConveyBuilder : IConveyBuilder
{
    private readonly ConcurrentDictionary<string, bool> _registry = [];
    private readonly List<Action<IServiceProvider>> _buildActions = [];

    public IServiceCollection Services { get; }
    public IConfiguration Configuration { get; }

    private ConveyBuilder(IServiceCollection services, IConfiguration configuration)
    {
        Services = services;
        Configuration = configuration;
    }

    public static IConveyBuilder Create(IServiceCollection services, IConfiguration configuration = null)
        => new ConveyBuilder(services, configuration);

    public bool TryRegister(string name) => _registry.TryAdd(name, true);

    public void AddBuildAction(Action<IServiceProvider> action)
        => _buildActions.Add(action);

    public IServiceProvider Build()
    {
        var serviceProvider = Services.BuildServiceProvider();
        _buildActions.ForEach(action => action.Invoke(serviceProvider));
        return serviceProvider;
    }
}