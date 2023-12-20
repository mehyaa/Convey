using Convey.Discovery.Consul.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Convey.Discovery.Consul.Services;

internal sealed class ConsulHostedService : IHostedService, IAsyncDisposable
{
    private readonly IConsulService _consulService;
    private readonly ServiceRegistration _serviceRegistration;
    private readonly ConsulOptions _consulOptions;
    private readonly ILogger<ConsulHostedService> _logger;

    private readonly CancellationTokenSource _cancellationTokenSource;

    private Timer _timer;

    public ConsulHostedService(
        IConsulService consulService,
        ServiceRegistration serviceRegistration,
        ConsulOptions consulOptions,
        ILogger<ConsulHostedService> logger)
    {
        _consulService = consulService;
        _serviceRegistration = serviceRegistration;
        _consulOptions = consulOptions;
        _logger = logger;

        _cancellationTokenSource = new CancellationTokenSource();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_consulOptions.Enabled)
        {
            return;
        }

        _logger.LogInformation("Registering service [id: {ServiceId}] to Consul...", _serviceRegistration.Id);

        var response = await _consulService.RegisterServiceAsync(_serviceRegistration, cancellationToken);

        try
        {
            response.EnsureSuccessStatusCode();

            _logger.LogInformation("Registered service [id: {ServiceId}] to Consul", _serviceRegistration.Id);
        }
        catch (Exception ex)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogError(ex, "There was an error when registering service [id: {ServiceId}] to Consul: {Response}", _serviceRegistration.Id, body);

            throw;
        }

        if (_consulOptions.TtlEnabled && _consulOptions.Ttl > 0)
        {
            var ttl = TimeSpan.FromSeconds(_consulOptions.Ttl <= 5 ? _consulOptions.Ttl * 0.8 : _consulOptions.Ttl - 2);

            _timer = new Timer(async _ =>
            {
                try
                {
                    if (!_cancellationTokenSource.IsCancellationRequested)
                    {
                        _logger.LogTrace("Passing TTL check for service [id: {ServiceId}] to Consul...", _serviceRegistration.Id);

                        await _consulService.PassCheckAsync(_serviceRegistration.Id, _cancellationTokenSource.Token);

                        _logger.LogTrace("TTL check for service [id: {ServiceId}] passed to Consul", _serviceRegistration.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "There was an error when passing TTL check for service [id: {ServiceId}] to Consul", _serviceRegistration.Id);
                }
            },
            null,
            ttl,
            ttl);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cancellationTokenSource.Cancel();

        if (!_consulOptions.Enabled)
        {
            return;
        }

        _logger.LogInformation("Deregistering service [id: {ServiceId}] from Consul...", _serviceRegistration.Id);

        var response = await _consulService.DeregisterServiceAsync(_serviceRegistration.Id, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            _logger.LogInformation("Deregistered service [id: {ServiceId}] from Consul", _serviceRegistration.Id);

            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        _logger.LogError("There was an error when deregistering service [id: {ServiceId}] from Consul: {Response}", _serviceRegistration.Id, body);
    }

    public async ValueTask DisposeAsync()
    {
        if (_timer is not null)
        {
            await _timer.DisposeAsync();
        }
    }
}