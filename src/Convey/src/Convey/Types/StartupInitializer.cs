using Microsoft.Extensions.Hosting;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Convey.Types;

public class StartupInitializer : IHostedService
{
    private readonly IEnumerable<IInitializer> _initializers;

    public StartupInitializer(IEnumerable<IInitializer> initializers)
    {
        _initializers = initializers;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var initializer in _initializers)
        {
            await initializer.InitializeAsync(cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}