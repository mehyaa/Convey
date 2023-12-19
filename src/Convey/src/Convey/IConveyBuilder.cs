using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Convey;

public interface IConveyBuilder
{
    IServiceCollection Services { get; }
    IConfiguration Configuration { get; }
    bool TryRegister(string name);
    void AddBuildAction(Action<IServiceProvider> action);
    IServiceProvider Build();
}