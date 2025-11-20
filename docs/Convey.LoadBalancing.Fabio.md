# Convey.LoadBalancing.Fabio

Load balancing integration using Fabio reverse proxy providing dynamic service discovery, health checking, and intelligent traffic distribution for microservices architectures.

## Installation

```bash
dotnet add package Convey.LoadBalancing.Fabio
```

## Overview

Convey.LoadBalancing.Fabio provides:
- **Fabio integration** - Dynamic reverse proxy and load balancer
- **Service discovery** - Automatic service registration via Consul
- **Health checking** - Continuous health monitoring and failover
- **Traffic routing** - URL-based routing and traffic distribution
- **Load balancing strategies** - Round robin, least connections, and custom algorithms
- **SSL termination** - HTTPS support and certificate management
- **Request metrics** - Performance monitoring and analytics
- **Circuit breakers** - Fault tolerance and resilience patterns

## Configuration

### Basic Fabio Setup

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddConsul()
    .AddFabio();

var app = builder.Build();
app.Run();
```

### Advanced Fabio Configuration

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddConsul()
    .AddHttpClient()
    .AddFabio(fabio =>
    {
        fabio.Enabled = true;
        fabio.Url = "http://fabio:9999";
        fabio.Service = "my-service";
        fabio.RequestRetries = 3;
        fabio.RequestTimeout = TimeSpan.FromSeconds(30);
    });

var app = builder.Build();
app.Run();
```

### Configuration with Custom Routes

```csharp
builder.Services.Configure<FabioOptions>(options =>
{
    options.Enabled = true;
    options.Url = "http://fabio-lb.example.com:9999";
    options.Service = "user-service";
    options.RequestRetries = 3;
    options.RequestTimeout = TimeSpan.FromSeconds(45);
    options.Tags = new[] { "api", "v1", "production" };
});

builder.Services.AddConvey()
    .AddConsul()
    .AddFabio()
    .AddHttpClient()
    .AddRestEaseClient<IUserService>("user-service", client =>
    {
        client.LoadBalancer = "fabio";
        client.Services = new[] { "user-service" };
    });
```

## Key Features

### 1. Service Registration with Fabio

Configure services for Fabio load balancing:

```csharp
// Service registration with Fabio tags
services.AddConvey()
    .AddConsul(consul =>
    {
        consul.Enabled = true;
        consul.Url = "http://consul:8500";
        consul.Service = "user-service";
        consul.Address = "user-service";
        consul.Port = 80;
        consul.PingEnabled = true;
        consul.PingEndpoint = "health";
        consul.PingInterval = 30;
        consul.RemoveAfterInterval = 60;
        consul.Tags = new[] { "urlprefix-/api/users" }; // Fabio routing tag
    })
    .AddFabio();

// Multi-route service registration
services.AddConvey()
    .AddConsul(consul =>
    {
        consul.Enabled = true;
        consul.Url = "http://consul:8500";
        consul.Service = "order-service";
        consul.Tags = new[]
        {
            "urlprefix-/api/orders",      // Main API routes
            "urlprefix-/api/orders/v1",   // Versioned routes
            "urlprefix-/webhooks/orders", // Webhook routes
            "strip=/api"                  // Strip prefix before forwarding
        };
    })
    .AddFabio();

// Service with custom load balancing
services.AddConvey()
    .AddConsul(consul =>
    {
        consul.Service = "payment-service";
        consul.Tags = new[]
        {
            "urlprefix-/api/payments",
            "urlprefix-/api/billing",
            "opts=route.strategy=rr",     // Round robin
            "opts=route.sticky=true"      // Sticky sessions
        };
    })
    .AddFabio();
```

### 2. HTTP Client Integration

Use Fabio with HTTP clients:

```csharp
// Service using Fabio load balancer
public class UserService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UserService> _logger;

    public UserService(HttpClient httpClient, ILogger<UserService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<UserDto> GetUserAsync(Guid userId)
    {
        try
        {
            // Request goes through Fabio load balancer
            var response = await _httpClient.GetAsync($"/api/users/{userId}");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var user = JsonSerializer.Deserialize<UserDto>(json);

            _logger.LogDebug("Retrieved user {UserId} via Fabio", userId);
            return user;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error retrieving user {UserId} through Fabio", userId);
            throw;
        }
    }

    public async Task<IEnumerable<UserDto>> GetUsersAsync(int page = 1, int pageSize = 20)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/users?page={page}&pageSize={pageSize}");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var users = JsonSerializer.Deserialize<IEnumerable<UserDto>>(json);

            _logger.LogDebug("Retrieved {Count} users via Fabio", users.Count());
            return users;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving users through Fabio");
            throw;
        }
    }

    public async Task<UserDto> CreateUserAsync(CreateUserRequest request)
    {
        try
        {
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/api/users", content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var user = JsonSerializer.Deserialize<UserDto>(responseJson);

            _logger.LogInformation("Created user {UserId} via Fabio", user.Id);
            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user through Fabio");
            throw;
        }
    }
}

// Order service with retry logic
public class OrderService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OrderService> _logger;

    public OrderService(HttpClient httpClient, ILogger<OrderService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<OrderDto> GetOrderAsync(Guid orderId)
    {
        var retryPolicy = Policy
            .Handle<HttpRequestException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning("Retry {RetryCount} for GetOrder({OrderId}) in {Delay}ms",
                        retryCount, orderId, timespan.TotalMilliseconds);
                });

        return await retryPolicy.ExecuteAsync(async () =>
        {
            var response = await _httpClient.GetAsync($"/api/orders/{orderId}");

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Order {OrderId} not found", orderId);
                return null;
            }

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var order = JsonSerializer.Deserialize<OrderDto>(json);

            _logger.LogDebug("Retrieved order {OrderId} via Fabio", orderId);
            return order;
        });
    }

    public async Task<OrderDto> CreateOrderAsync(CreateOrderRequest request)
    {
        try
        {
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/api/orders", content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var order = JsonSerializer.Deserialize<OrderDto>(responseJson);

            _logger.LogInformation("Created order {OrderId} via Fabio load balancer", order.Id);
            return order;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating order through Fabio");
            throw;
        }
    }

    public async Task<bool> UpdateOrderStatusAsync(Guid orderId, OrderStatus status)
    {
        try
        {
            var request = new { Status = status };
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PutAsync($"/api/orders/{orderId}/status", content);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Order {OrderId} not found for status update", orderId);
                return false;
            }

            response.EnsureSuccessStatusCode();
            _logger.LogInformation("Updated order {OrderId} status to {Status} via Fabio", orderId, status);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating order {OrderId} status through Fabio", orderId);
            throw;
        }
    }
}

// Payment service with circuit breaker
public class PaymentService
{
    private readonly HttpClient _httpClient;
    private readonly ICircuitBreaker _circuitBreaker;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(HttpClient httpClient, ILogger<PaymentService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _circuitBreaker = Policy
            .Handle<HttpRequestException>()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 3,
                durationOfBreak: TimeSpan.FromMinutes(1),
                onBreak: (exception, duration) =>
                {
                    _logger.LogWarning("Payment service circuit breaker opened for {Duration}ms", duration.TotalMilliseconds);
                },
                onReset: () => _logger.LogInformation("Payment service circuit breaker reset"));
    }

    public async Task<PaymentResult> ProcessPaymentAsync(ProcessPaymentRequest request)
    {
        try
        {
            return await _circuitBreaker.ExecuteAsync(async () =>
            {
                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/api/payments/process", content);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<PaymentResult>(responseJson);

                _logger.LogInformation("Processed payment {PaymentId} for order {OrderId} via Fabio",
                    result.PaymentId, request.OrderId);

                return result;
            });
        }
        catch (CircuitBreakerOpenException)
        {
            _logger.LogError("Payment service circuit breaker is open, cannot process payment for order {OrderId}",
                request.OrderId);

            return new PaymentResult
            {
                Success = false,
                ErrorMessage = "Payment service temporarily unavailable"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing payment for order {OrderId} through Fabio", request.OrderId);
            throw;
        }
    }
}
```

### 3. RestEase Integration

Combine Fabio with RestEase clients:

```csharp
// User API client with Fabio
[Header("User-Agent", "MyApp/1.0")]
public interface IUserApiClient
{
    [Get("api/users/{id}")]
    Task<UserDto> GetUserAsync([Path] Guid id);

    [Get("api/users")]
    Task<PagedResult<UserDto>> GetUsersAsync([Query] GetUsersQuery query);

    [Post("api/users")]
    Task<UserDto> CreateUserAsync([Body] CreateUserRequest request);

    [Put("api/users/{id}")]
    Task UpdateUserAsync([Path] Guid id, [Body] UpdateUserRequest request);

    [Delete("api/users/{id}")]
    Task DeleteUserAsync([Path] Guid id);
}

// Order API client with Fabio
public interface IOrderApiClient
{
    [Get("api/orders/{id}")]
    Task<OrderDto> GetOrderAsync([Path] Guid id);

    [Post("api/orders")]
    Task<OrderDto> CreateOrderAsync([Body] CreateOrderRequest request);

    [Put("api/orders/{id}/status")]
    Task UpdateOrderStatusAsync([Path] Guid id, [Body] UpdateOrderStatusRequest request);

    [Get("api/orders/reports/daily")]
    Task<DailyOrderReport> GetDailyReportAsync([Query] DateTime date);
}

// Service registration with Fabio load balancer
builder.Services.AddConvey()
    .AddConsul()
    .AddFabio()
    .AddHttpClient()
    .AddRestEaseClient<IUserApiClient>("user-service", client =>
    {
        client.LoadBalancer = "fabio"; // Use Fabio for load balancing
        client.Services = new[] { "user-service" };
    })
    .AddRestEaseClient<IOrderApiClient>("order-service", client =>
    {
        client.LoadBalancer = "fabio";
        client.Services = new[] { "order-service" };
    });

// Service using RestEase clients with Fabio
public class UserManagementService
{
    private readonly IUserApiClient _userClient;
    private readonly IOrderApiClient _orderClient;
    private readonly ILogger<UserManagementService> _logger;

    public UserManagementService(
        IUserApiClient userClient,
        IOrderApiClient orderClient,
        ILogger<UserManagementService> logger)
    {
        _userClient = userClient;
        _orderClient = orderClient;
        _logger = logger;
    }

    public async Task<UserWithOrdersDto> GetUserWithOrdersAsync(Guid userId)
    {
        try
        {
            // Both calls go through Fabio load balancer
            var userTask = _userClient.GetUserAsync(userId);
            var ordersTask = GetUserOrdersAsync(userId);

            await Task.WhenAll(userTask, ordersTask);

            var user = userTask.Result;
            var orders = ordersTask.Result;

            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found", userId);
                return null;
            }

            _logger.LogDebug("Retrieved user {UserId} with {OrderCount} orders via Fabio",
                userId, orders.Count());

            return new UserWithOrdersDto
            {
                User = user,
                Orders = orders
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user {UserId} with orders via Fabio", userId);
            throw;
        }
    }

    private async Task<IEnumerable<OrderDto>> GetUserOrdersAsync(Guid userId)
    {
        try
        {
            var query = new GetUsersQuery { UserId = userId };
            var result = await _orderClient.GetOrdersAsync(query);
            return result.Items;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error retrieving orders for user {UserId}", userId);
            return Enumerable.Empty<OrderDto>();
        }
    }
}
```

### 4. Health Checks and Monitoring

Implement health checks for Fabio integration:

```csharp
// Health check for Fabio connectivity
public class FabioHealthCheck : IHealthCheck
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<FabioOptions> _options;
    private readonly ILogger<FabioHealthCheck> _logger;

    public FabioHealthCheck(HttpClient httpClient, IOptions<FabioOptions> options, ILogger<FabioHealthCheck> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_options.Value.Enabled)
            {
                return HealthCheckResult.Healthy("Fabio is disabled");
            }

            var response = await _httpClient.GetAsync($"{_options.Value.Url}/health", cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Fabio health check successful");
                return HealthCheckResult.Healthy($"Fabio is healthy at {_options.Value.Url}");
            }

            _logger.LogWarning("Fabio health check failed with status {StatusCode}", response.StatusCode);
            return HealthCheckResult.Unhealthy($"Fabio returned {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fabio health check failed");
            return HealthCheckResult.Unhealthy($"Fabio health check failed: {ex.Message}");
        }
    }
}

// Service monitoring
public class FabioMonitoringService : BackgroundService
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<FabioOptions> _options;
    private readonly ILogger<FabioMonitoringService> _logger;

    public FabioMonitoringService(HttpClient httpClient, IOptions<FabioOptions> options, ILogger<FabioMonitoringService> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckFabioStatusAsync();
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Fabio monitoring service");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }

    private async Task CheckFabioStatusAsync()
    {
        try
        {
            // Check Fabio routes
            var routesResponse = await _httpClient.GetAsync($"{_options.Value.Url}/routes");
            if (routesResponse.IsSuccessStatusCode)
            {
                var routes = await routesResponse.Content.ReadAsStringAsync();
                _logger.LogDebug("Fabio routes: {Routes}", routes);
            }

            // Check Fabio metrics
            var metricsResponse = await _httpClient.GetAsync($"{_options.Value.Url}/metrics");
            if (metricsResponse.IsSuccessStatusCode)
            {
                var metrics = await metricsResponse.Content.ReadAsStringAsync();
                _logger.LogDebug("Fabio metrics retrieved successfully");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve Fabio status information");
        }
    }
}

// Register health checks and monitoring
builder.Services.AddHealthChecks()
    .AddCheck<FabioHealthCheck>("fabio");

builder.Services.AddHostedService<FabioMonitoringService>();
```

### 5. Advanced Routing Configuration

Configure advanced Fabio routing scenarios:

```csharp
// Multi-version API routing
services.AddConvey()
    .AddConsul(consul =>
    {
        consul.Service = "api-gateway";
        consul.Tags = new[]
        {
            // Version 1 API routes
            "urlprefix-/api/v1/users route=v1-users",
            "urlprefix-/api/v1/orders route=v1-orders",

            // Version 2 API routes
            "urlprefix-/api/v2/users route=v2-users",
            "urlprefix-/api/v2/orders route=v2-orders",

            // Default to v2
            "urlprefix-/api/users route=v2-users",
            "urlprefix-/api/orders route=v2-orders",

            // Load balancing strategy
            "opts=route.strategy=rr",
            "opts=route.weight=100"
        };
    })
    .AddFabio();

// Canary deployment routing
services.AddConvey()
    .AddConsul(consul =>
    {
        consul.Service = "user-service-canary";
        consul.Tags = new[]
        {
            "urlprefix-/api/users",
            "opts=route.weight=10",      // 10% traffic to canary
            "opts=route.tags=canary"
        };
    })
    .AddFabio();

// Geographic routing
services.AddConvey()
    .AddConsul(consul =>
    {
        consul.Service = "payment-service-eu";
        consul.Tags = new[]
        {
            "urlprefix-/api/payments",
            "opts=route.match=Header(`X-Region`, `EU`)",
            "opts=route.priority=100"
        };
    })
    .AddFabio();

// Rate limiting and security
services.AddConvey()
    .AddConsul(consul =>
    {
        consul.Service = "public-api";
        consul.Tags = new[]
        {
            "urlprefix-/public/api",
            "opts=route.ratelimit=100req/s",
            "opts=route.auth=basic",
            "opts=route.cors=true"
        };
    })
    .AddFabio();
```

## Configuration Options

### Fabio Options

```csharp
public class FabioOptions
{
    public bool Enabled { get; set; } = true;
    public string Url { get; set; } = "http://localhost:9999";
    public string Service { get; set; }
    public int RequestRetries { get; set; } = 3;
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public string[] Tags { get; set; } = new string[0];
}
```

### Consul Integration

```csharp
builder.Services.Configure<ConsulOptions>(options =>
{
    options.Enabled = true;
    options.Url = "http://consul:8500";
    options.Service = "my-service";
    options.Tags = new[]
    {
        "urlprefix-/api/myservice",
        "strip=/api",
        "opts=route.strategy=rr"
    };
});
```

## API Reference

### Extension Methods

```csharp
public static class ConveyExtensions
{
    public static IConveyBuilder AddFabio(this IConveyBuilder builder);
    public static IConveyBuilder AddFabio(this IConveyBuilder builder, Action<FabioOptions> configure);
}
```

## Best Practices

1. **Use service discovery** - Register services with appropriate Fabio tags
2. **Configure health checks** - Monitor Fabio connectivity and status
3. **Implement retry policies** - Handle load balancer failures gracefully
4. **Use correlation IDs** - Track requests across load-balanced services
5. **Monitor performance** - Track latency and error rates through Fabio
6. **Configure timeouts** - Set appropriate request timeout values
7. **Use circuit breakers** - Protect against cascading failures
8. **Plan for failover** - Configure multiple Fabio instances for high availability

## Troubleshooting

### Common Issues

1. **Service not found**
   - Check Consul service registration
   - Verify Fabio routing tags
   - Ensure service health checks pass

2. **Load balancing not working**
   - Verify Fabio configuration
   - Check service tags and routes
   - Monitor Fabio logs for errors

3. **High latency**
   - Check network connectivity
   - Monitor service instance health
   - Adjust load balancing strategy

4. **SSL/TLS issues**
   - Verify certificate configuration
   - Check SSL termination settings
   - Ensure proper HTTPS routing
