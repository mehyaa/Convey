---
layout: default
title: Convey.Discovery.Consul
parent: Service Discovery & Load Balancing
---
# Convey.Discovery.Consul

Service discovery integration with HashiCorp Consul providing automatic service registration, health checking, load balancing, and dynamic service resolution for microservices architectures.

## Installation

```bash
dotnet add package Convey.Discovery.Consul
```

## Overview

Convey.Discovery.Consul provides:
- **Service registration** - Automatic service registration with Consul
- **Service discovery** - Dynamic service resolution and load balancing
- **Health checking** - Automatic health check registration and monitoring
- **Configuration integration** - Consul KV store integration for configuration
- **Load balancing** - Client-side load balancing with multiple strategies
- **Failover support** - Automatic failover and circuit breaker patterns
- **Tag-based routing** - Service routing based on tags and metadata

## Configuration

### Basic Setup

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddConsul(); // Enables Consul integration and automatic service registration

var app = builder.Build();

app.Run(); // Service is automatically registered with Consul on startup
```

### Advanced Configuration

Configure in `appsettings.json`:

```json
{
  "consul": {
    "enabled": true,
    "url": "http://localhost:8500",
    "service": "my-service",
    "address": "localhost",
    "port": 5000,
    "pingEnabled": true,
    "pingEndpoint": "health",
    "pingInterval": 5,
    "removeAfterInterval": 10,
    "requestRetries": 3,
    "tags": ["api", "v1", "production"],
    "meta": {
      "version": "1.0.0",
      "description": "My microservice"
    },
    "enableTagOverride": false,
    "connect": {
      "enabled": false
    }
  }
}
```

### Full Configuration

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddConsul(consulOptions =>
    {
        consulOptions.Url = "http://consul-server:8500";
        consulOptions.Service = "order-service";
        consulOptions.Address = Environment.GetEnvironmentVariable("SERVICE_ADDRESS") ?? "localhost";
        consulOptions.Port = int.Parse(Environment.GetEnvironmentVariable("SERVICE_PORT") ?? "5000");
        consulOptions.Tags = new[] { "orders", "api", "v1" };
        consulOptions.PingEnabled = true;
        consulOptions.PingEndpoint = "health";
        consulOptions.PingInterval = 10;
    })
    .AddConsulHttpClient(); // Adds HTTP client with service discovery

var app = builder.Build();

app.Run(); // Service is automatically registered with Consul
```

## Key Features

### 1. Automatic Service Registration

Services are automatically registered with Consul:

```csharp
// Service registration happens automatically when the application starts
// through the ConsulHostedService that is registered by AddConsul()

// Manual registration (if needed for custom scenarios)
public class ConsulServiceRegistry
{
    private readonly IConsulClient _consulClient;
    private readonly ConsulOptions _options;

    public ConsulServiceRegistry(IConsulClient consulClient, ConsulOptions options)
    {
        _consulClient = consulClient;
        _options = options;
    }

    public async Task RegisterServiceAsync()
    {
        var registration = new AgentServiceRegistration
        {
            ID = $"{_options.Service}-{Environment.MachineName}-{_options.Port}",
            Name = _options.Service,
            Address = _options.Address,
            Port = _options.Port,
            Tags = _options.Tags,
            Meta = _options.Meta,
            EnableTagOverride = _options.EnableTagOverride,
            Check = new AgentServiceCheck
            {
                HTTP = $"http://{_options.Address}:{_options.Port}/{_options.PingEndpoint}",
                Interval = TimeSpan.FromSeconds(_options.PingInterval),
                DeregisterCriticalServiceAfter = TimeSpan.FromSeconds(_options.RemoveAfterInterval)
            }
        };

        await _consulClient.Agent.ServiceRegister(registration);
    }
}
```

### 2. Service Discovery and Load Balancing

Discover and connect to other services:

```csharp
// Service discovery client
public class UserServiceClient
{
    private readonly IConsulClient _consulClient;
    private readonly HttpClient _httpClient;

    public UserServiceClient(IConsulClient consulClient, HttpClient httpClient)
    {
        _consulClient = consulClient;
        _httpClient = httpClient;
    }

    public async Task<User> GetUserAsync(Guid userId)
    {
        // Discover user service instances
        var services = await _consulClient.Health.Service("user-service", string.Empty, true);

        if (!services.Response.Any())
        {
            throw new ServiceUnavailableException("User service not available");
        }

        // Load balancing - round robin
        var service = services.Response[Random.Next(services.Response.Length)];
        var serviceUrl = $"http://{service.Service.Address}:{service.Service.Port}";

        // Make HTTP request
        var response = await _httpClient.GetAsync($"{serviceUrl}/api/users/{userId}");
        response.EnsureSuccessStatusCode();

        var userJson = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<User>(userJson);
    }
}

// Service discovery with load balancing strategies
public interface ILoadBalancer
{
    ServiceInstance SelectInstance(IEnumerable<ServiceInstance> instances);
}

public class RoundRobinLoadBalancer : ILoadBalancer
{
    private int _counter = 0;

    public ServiceInstance SelectInstance(IEnumerable<ServiceInstance> instances)
    {
        var instanceArray = instances.ToArray();
        if (!instanceArray.Any())
            throw new InvalidOperationException("No service instances available");

        var index = Interlocked.Increment(ref _counter) % instanceArray.Length;
        return instanceArray[index];
    }
}

public class RandomLoadBalancer : ILoadBalancer
{
    private readonly Random _random = new Random();

    public ServiceInstance SelectInstance(IEnumerable<ServiceInstance> instances)
    {
        var instanceArray = instances.ToArray();
        if (!instanceArray.Any())
            throw new InvalidOperationException("No service instances available");

        var index = _random.Next(instanceArray.Length);
        return instanceArray[index];
    }
}

public class WeightedRoundRobinLoadBalancer : ILoadBalancer
{
    private readonly Dictionary<string, int> _weights = new();
    private int _currentIndex = 0;

    public ServiceInstance SelectInstance(IEnumerable<ServiceInstance> instances)
    {
        var instanceArray = instances.ToArray();
        if (!instanceArray.Any())
            throw new InvalidOperationException("No service instances available");

        // Implementation of weighted round robin logic
        return instanceArray[_currentIndex++ % instanceArray.Length];
    }
}
```

### 3. Health Checking

Implement health checks for service monitoring:

```csharp
// Health check endpoint
[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    private readonly IServiceHealthChecker _healthChecker;

    public HealthController(IServiceHealthChecker healthChecker)
    {
        _healthChecker = healthChecker;
    }

    [HttpGet]
    public async Task<IActionResult> CheckHealth()
    {
        var healthResult = await _healthChecker.CheckHealthAsync();

        if (healthResult.IsHealthy)
        {
            return Ok(new { status = "healthy", checks = healthResult.Checks });
        }

        return StatusCode(503, new { status = "unhealthy", checks = healthResult.Checks });
    }
}

// Service health checker
public interface IServiceHealthChecker
{
    Task<HealthResult> CheckHealthAsync();
}

public class ServiceHealthChecker : IServiceHealthChecker
{
    private readonly IDatabase _database;
    private readonly IMessageBroker _messageBroker;
    private readonly IExternalApiClient _externalApiClient;

    public ServiceHealthChecker(
        IDatabase database,
        IMessageBroker messageBroker,
        IExternalApiClient externalApiClient)
    {
        _database = database;
        _messageBroker = messageBroker;
        _externalApiClient = externalApiClient;
    }

    public async Task<HealthResult> CheckHealthAsync()
    {
        var checks = new List<HealthCheck>();

        // Database health check
        try
        {
            await _database.PingAsync();
            checks.Add(new HealthCheck("database", "healthy", "Database connection successful"));
        }
        catch (Exception ex)
        {
            checks.Add(new HealthCheck("database", "unhealthy", ex.Message));
        }

        // Message broker health check
        try
        {
            await _messageBroker.PingAsync();
            checks.Add(new HealthCheck("message_broker", "healthy", "Message broker connection successful"));
        }
        catch (Exception ex)
        {
            checks.Add(new HealthCheck("message_broker", "unhealthy", ex.Message));
        }

        // External API health check
        try
        {
            await _externalApiClient.HealthCheckAsync();
            checks.Add(new HealthCheck("external_api", "healthy", "External API accessible"));
        }
        catch (Exception ex)
        {
            checks.Add(new HealthCheck("external_api", "unhealthy", ex.Message));
        }

        var isHealthy = checks.All(c => c.Status == "healthy");
        return new HealthResult(isHealthy, checks);
    }
}

public class HealthResult
{
    public bool IsHealthy { get; }
    public IEnumerable<HealthCheck> Checks { get; }

    public HealthResult(bool isHealthy, IEnumerable<HealthCheck> checks)
    {
        IsHealthy = isHealthy;
        Checks = checks;
    }
}

public class HealthCheck
{
    public string Name { get; }
    public string Status { get; }
    public string Description { get; }

    public HealthCheck(string name, string status, string description)
    {
        Name = name;
        Status = status;
        Description = description;
    }
}
```

### 4. Configuration from Consul KV Store

Use Consul's key-value store for configuration:

```csharp
// Consul configuration provider
public class ConsulConfigurationProvider : ConfigurationProvider, IDisposable
{
    private readonly IConsulClient _consulClient;
    private readonly string _keyPrefix;
    private readonly Timer _refreshTimer;

    public ConsulConfigurationProvider(IConsulClient consulClient, string keyPrefix)
    {
        _consulClient = consulClient;
        _keyPrefix = keyPrefix;
        _refreshTimer = new Timer(RefreshConfiguration, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
    }

    public override void Load()
    {
        LoadFromConsul().GetAwaiter().GetResult();
    }

    private async Task LoadFromConsul()
    {
        try
        {
            var kvPairs = await _consulClient.KV.List(_keyPrefix);

            if (kvPairs.Response != null)
            {
                Data = kvPairs.Response.ToDictionary(
                    kvp => kvp.Key.Substring(_keyPrefix.Length + 1).Replace('/', ':'),
                    kvp => Encoding.UTF8.GetString(kvp.Value ?? new byte[0]));
            }
        }
        catch (Exception ex)
        {
            // Log error but don't fail the application
            Console.WriteLine($"Failed to load configuration from Consul: {ex.Message}");
        }
    }

    private async void RefreshConfiguration(object state)
    {
        var previousData = Data;
        await LoadFromConsul();

        if (!Data.SequenceEqual(previousData))
        {
            OnReload();
        }
    }

    public void Dispose()
    {
        _refreshTimer?.Dispose();
    }
}

// Configuration extension
public static class ConsulConfigurationExtensions
{
    public static IConfigurationBuilder AddConsul(
        this IConfigurationBuilder builder,
        string consulUrl,
        string keyPrefix)
    {
        var consulClient = new ConsulClient(config =>
        {
            config.Address = new Uri(consulUrl);
        });

        return builder.Add(new ConsulConfigurationSource(consulClient, keyPrefix));
    }
}

// Usage in Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddConsul("http://localhost:8500", "config/my-service");

// Configuration will be automatically loaded from Consul KV store
// Keys like "config/my-service/database/connectionString" become "database:connectionString"
```

### 5. Circuit Breaker Pattern

Implement circuit breaker for service calls:

```csharp
public class CircuitBreakerServiceClient
{
    private readonly IConsulClient _consulClient;
    private readonly HttpClient _httpClient;
    private readonly ICircuitBreaker _circuitBreaker;

    public CircuitBreakerServiceClient(
        IConsulClient consulClient,
        HttpClient httpClient,
        ICircuitBreaker circuitBreaker)
    {
        _consulClient = consulClient;
        _httpClient = httpClient;
        _circuitBreaker = circuitBreaker;
    }

    public async Task<T> CallServiceAsync<T>(string serviceName, Func<string, Task<T>> serviceCall)
    {
        return await _circuitBreaker.ExecuteAsync(async () =>
        {
            // Discover healthy service instances
            var services = await _consulClient.Health.Service(serviceName, string.Empty, true);

            if (!services.Response.Any())
            {
                throw new ServiceUnavailableException($"No healthy instances of {serviceName} available");
            }

            // Try each instance until one succeeds
            Exception lastException = null;

            foreach (var service in services.Response)
            {
                try
                {
                    var serviceUrl = $"http://{service.Service.Address}:{service.Service.Port}";
                    return await serviceCall(serviceUrl);
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    // Continue to next instance
                }
            }

            throw lastException ?? new ServiceUnavailableException($"All instances of {serviceName} failed");
        });
    }
}

// Circuit breaker implementation
public interface ICircuitBreaker
{
    Task<T> ExecuteAsync<T>(Func<Task<T>> operation);
}

public class CircuitBreaker : ICircuitBreaker
{
    private readonly int _failureThreshold;
    private readonly TimeSpan _timeout;
    private int _failureCount = 0;
    private DateTime _lastFailureTime = DateTime.MinValue;
    private CircuitBreakerState _state = CircuitBreakerState.Closed;

    public CircuitBreaker(int failureThreshold = 5, TimeSpan? timeout = null)
    {
        _failureThreshold = failureThreshold;
        _timeout = timeout ?? TimeSpan.FromMinutes(1);
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        if (_state == CircuitBreakerState.Open)
        {
            if (DateTime.UtcNow - _lastFailureTime > _timeout)
            {
                _state = CircuitBreakerState.HalfOpen;
            }
            else
            {
                throw new CircuitBreakerOpenException("Circuit breaker is open");
            }
        }

        try
        {
            var result = await operation();

            if (_state == CircuitBreakerState.HalfOpen)
            {
                _state = CircuitBreakerState.Closed;
                _failureCount = 0;
            }

            return result;
        }
        catch (Exception ex)
        {
            _failureCount++;
            _lastFailureTime = DateTime.UtcNow;

            if (_failureCount >= _failureThreshold)
            {
                _state = CircuitBreakerState.Open;
            }

            throw;
        }
    }
}

public enum CircuitBreakerState
{
    Closed,
    Open,
    HalfOpen
}

public class CircuitBreakerOpenException : Exception
{
    public CircuitBreakerOpenException(string message) : base(message) { }
}
```

## Advanced Features

### 1. Service Mesh Integration

Integrate with Consul Connect for service mesh:

```csharp
// Consul Connect configuration
public class ConsulConnectOptions
{
    public bool Enabled { get; set; } = false;
    public string ProxyId { get; set; }
    public int ProxyPort { get; set; } = 21000;
    public Dictionary<string, object> ProxyConfig { get; set; } = new();
}

// Service registration with Connect
public async Task RegisterServiceWithConnectAsync()
{
    var registration = new AgentServiceRegistration
    {
        ID = $"{_options.Service}-{Environment.MachineName}-{_options.Port}",
        Name = _options.Service,
        Address = _options.Address,
        Port = _options.Port,
        Tags = _options.Tags,
        Connect = new AgentServiceConnect
        {
            Native = false,
            Proxy = new AgentServiceConnectProxy
            {
                DestinationServiceName = _options.Service,
                DestinationServiceID = $"{_options.Service}-{Environment.MachineName}-{_options.Port}",
                LocalServiceAddress = _options.Address,
                LocalServicePort = _options.Port
            }
        }
    };

    await _consulClient.Agent.ServiceRegister(registration);
}
```

### 2. Multi-Datacenter Support

Support for multi-datacenter Consul deployments:

```csharp
public class MultiDatacenterConsulClient
{
    private readonly Dictionary<string, IConsulClient> _consulClients;

    public MultiDatacenterConsulClient(Dictionary<string, string> datacenterUrls)
    {
        _consulClients = datacenterUrls.ToDictionary(
            kvp => kvp.Key,
            kvp => new ConsulClient(config => config.Address = new Uri(kvp.Value)) as IConsulClient);
    }

    public async Task<IEnumerable<ServiceEntry>> DiscoverServiceAsync(string serviceName, string datacenter = null)
    {
        if (datacenter != null && _consulClients.TryGetValue(datacenter, out var specificClient))
        {
            var result = await specificClient.Health.Service(serviceName, string.Empty, true);
            return result.Response;
        }

        // Search across all datacenters
        var allServices = new List<ServiceEntry>();

        foreach (var (dc, client) in _consulClients)
        {
            try
            {
                var result = await client.Health.Service(serviceName, string.Empty, true);
                allServices.AddRange(result.Response);
            }
            catch (Exception ex)
            {
                // Log but continue with other datacenters
                Console.WriteLine($"Failed to query datacenter {dc}: {ex.Message}");
            }
        }

        return allServices;
    }
}
```

### 3. Dynamic Configuration Updates

Handle dynamic configuration changes:

```csharp
public class DynamicConfigurationService : IHostedService, IDisposable
{
    private readonly IConsulClient _consulClient;
    private readonly IOptionsMonitor<AppSettings> _optionsMonitor;
    private readonly ILogger<DynamicConfigurationService> _logger;
    private readonly Timer _configCheckTimer;

    public DynamicConfigurationService(
        IConsulClient consulClient,
        IOptionsMonitor<AppSettings> optionsMonitor,
        ILogger<DynamicConfigurationService> logger)
    {
        _consulClient = consulClient;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
        _configCheckTimer = new Timer(CheckConfigurationChanges, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Dynamic configuration service started");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _configCheckTimer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    private async void CheckConfigurationChanges(object state)
    {
        try
        {
            // Check for configuration changes in Consul
            var configKey = "config/my-service/settings";
            var kvResult = await _consulClient.KV.Get(configKey);

            if (kvResult.Response != null)
            {
                var configJson = Encoding.UTF8.GetString(kvResult.Response.Value);
                var newSettings = JsonSerializer.Deserialize<AppSettings>(configJson);

                // Compare with current settings and apply if different
                var currentSettings = _optionsMonitor.CurrentValue;
                if (!SettingsEqual(currentSettings, newSettings))
                {
                    _logger.LogInformation("Configuration changes detected, applying updates");
                    await ApplyConfigurationChanges(newSettings);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking configuration changes");
        }
    }

    private async Task ApplyConfigurationChanges(AppSettings newSettings)
    {
        // Apply configuration changes
        // This might involve restarting services, updating connection strings, etc.
        _logger.LogInformation("Applied configuration changes: {@Settings}", newSettings);
    }

    private bool SettingsEqual(AppSettings current, AppSettings updated)
    {
        // Compare settings to determine if changes occurred
        return JsonSerializer.Serialize(current) == JsonSerializer.Serialize(updated);
    }

    public void Dispose()
    {
        _configCheckTimer?.Dispose();
    }
}
```

## Configuration Options

### Consul Settings

```csharp
public class ConsulOptions
{
    public bool Enabled { get; set; } = true;
    public string Url { get; set; } = "http://localhost:8500";
    public string Service { get; set; }
    public string Address { get; set; } = "localhost";
    public int Port { get; set; }
    public bool PingEnabled { get; set; } = true;
    public string PingEndpoint { get; set; } = "health";
    public int PingInterval { get; set; } = 5;
    public int RemoveAfterInterval { get; set; } = 10;
    public int RequestRetries { get; set; } = 3;
    public string[] Tags { get; set; } = Array.Empty<string>();
    public Dictionary<string, string> Meta { get; set; } = new();
    public bool EnableTagOverride { get; set; } = false;
    public ConsulConnectOptions Connect { get; set; } = new();
}
```

## API Reference

### IConsulClient Extensions

```csharp
public static class ConsulExtensions
{
    public static Task<QueryResult<ServiceEntry[]>> GetHealthyServicesAsync(this IConsulClient client, string serviceName);
    public static Task<ServiceEntry> GetServiceInstanceAsync(this IConsulClient client, string serviceName, ILoadBalancer loadBalancer);
    public static Task RegisterServiceAsync(this IConsulClient client, string serviceName, string address, int port, string[] tags = null);
    public static Task DeregisterServiceAsync(this IConsulClient client, string serviceId);
}
```

### Extension Methods

```csharp
public static class ConveyExtensions
{
    public static IConveyBuilder AddConsul(this IConveyBuilder builder, string sectionName = "consul");
    public static IConveyBuilder AddConsul(this IConveyBuilder builder, Func<IConsulOptionsBuilder, IConsulOptionsBuilder> buildOptions, HttpClientOptions httpClientOptions);
    public static IConveyBuilder AddConsul(this IConveyBuilder builder, ConsulOptions options, HttpClientOptions httpClientOptions);
    public static void AddConsulHttpClient(this IConveyBuilder builder, string clientName, string serviceName);
}
```

## Best Practices

1. **Use health checks** - Implement comprehensive health checks for reliable service discovery
2. **Tag services appropriately** - Use meaningful tags for service routing and filtering
3. **Handle failures gracefully** - Implement circuit breakers and retry policies
4. **Monitor service health** - Set up monitoring for service registration and discovery
5. **Use load balancing** - Implement appropriate load balancing strategies
6. **Secure Consul** - Use ACLs and TLS for production deployments
7. **Plan for network partitions** - Handle Consul unavailability scenarios
8. **Version your services** - Use tags or metadata for service versioning

## Troubleshooting

### Common Issues

1. **Service not registering**
   - Check Consul agent connectivity
   - Verify service configuration
   - Check for port conflicts

2. **Health checks failing**
   - Verify health check endpoint accessibility
   - Check health check interval and timeout settings
   - Review application logs for health check errors

3. **Service discovery failures**
   - Ensure services are registered and healthy
   - Check Consul cluster health
   - Verify load balancing configuration

4. **Configuration not updating**
   - Check Consul KV permissions
   - Verify configuration key paths
   - Monitor configuration refresh intervals

