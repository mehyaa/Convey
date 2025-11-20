# Convey.Metrics.Prometheus

Prometheus metrics integration providing comprehensive application monitoring, performance tracking, and observability with custom metrics, built-in collectors, and dashboard support.

## Installation

```bash
dotnet add package Convey.Metrics.Prometheus
```

## Overview

Convey.Metrics.Prometheus provides:
- **Prometheus integration** - Native Prometheus metrics collection and exposition
- **Built-in metrics** - HTTP request metrics, system metrics, and application metrics
- **Custom metrics** - Support for custom counters, gauges, histograms, and summaries
- **Automatic collection** - Middleware for automatic HTTP request tracking
- **Performance optimized** - High-performance metrics collection with minimal overhead
- **Dashboard ready** - Compatible with Grafana and other monitoring dashboards

## Configuration

### Basic Setup

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddMetrics(); // Enables Prometheus metrics

var app = builder.Build();

// Enable metrics endpoint
app.UseMetrics(); // Exposes /metrics endpoint

app.Run();
```

### Advanced Configuration

Configure in `appsettings.json`:

```json
{
  "metrics": {
    "enabled": true,
    "prometheusEnabled": true,
    "prometheusFormatter": "prometheus",
    "influxEnabled": false,
    "tags": {
      "application": "MyService",
      "environment": "production",
      "version": "1.0.0"
    }
  }
}
```

### Full Configuration with Middleware

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddMetrics();

var app = builder.Build();

app.UseRouting();
app.UsePrometheus();  // Registers the Prometheus metrics middleware

app.MapControllers();
app.Run();
```

## Key Features

### 1. Built-in HTTP Metrics

Automatic HTTP request/response metrics collection:

```csharp
app.UsePrometheus();
```

### 2. Custom Metrics

Create and use custom metrics:

```csharp
// Service with custom metrics
public class OrderService
{
    private readonly IMetrics _metrics;
    private readonly Counter _ordersProcessed;
    private readonly Gauge _activeOrders;
    private readonly Histogram _orderProcessingDuration;
    private readonly Summary _orderAmount;

    public OrderService(IMetrics metrics)
    {
        _metrics = metrics;

        // Counter - monotonically increasing
        _ordersProcessed = _metrics.CreateCounter(
            "orders_processed_total",
            "Total number of orders processed",
            "status", "type");

        // Gauge - can go up and down
        _activeOrders = _metrics.CreateGauge(
            "active_orders",
            "Number of orders currently being processed");

        // Histogram - distribution of values
        _orderProcessingDuration = _metrics.CreateHistogram(
            "order_processing_duration_seconds",
            "Time spent processing orders",
            new[] { 0.1, 0.5, 1.0, 2.0, 5.0, 10.0 });

        // Summary - distribution with quantiles
        _orderAmount = _metrics.CreateSummary(
            "order_amount_dollars",
            "Order amounts in dollars",
            new[] { 0.5, 0.9, 0.95, 0.99 });
    }

    public async Task<OrderResult> ProcessOrderAsync(ProcessOrderCommand command)
    {
        _activeOrders.Inc(); // Increment active orders

        using var timer = _orderProcessingDuration.NewTimer(); // Track duration

        try
        {
            var result = await ProcessOrder(command);

            // Record successful processing
            _ordersProcessed.WithTags("status", "success", "type", command.Type).Inc();
            _orderAmount.Observe(command.Amount);

            return result;
        }
        catch (ValidationException)
        {
            _ordersProcessed.WithTags("status", "validation_error", "type", command.Type).Inc();
            throw;
        }
        catch (Exception)
        {
            _ordersProcessed.WithTags("status", "error", "type", command.Type).Inc();
            throw;
        }
        finally
        {
            _activeOrders.Dec(); // Decrement active orders
        }
    }
}

// Registration
builder.Services.AddScoped<OrderService>();
```

### 3. Business Metrics

Track business-specific metrics:

```csharp
public class BusinessMetricsService
{
    private readonly IMetrics _metrics;
    private readonly Counter _userRegistrations;
    private readonly Counter _subscriptions;
    private readonly Gauge _monthlyRevenue;
    private readonly Counter _featureUsage;
    private readonly Histogram _userSessionDuration;

    public BusinessMetricsService(IMetrics metrics)
    {
        _metrics = metrics;

        _userRegistrations = _metrics.CreateCounter(
            "user_registrations_total",
            "Total user registrations",
            "source", "plan");

        _subscriptions = _metrics.CreateCounter(
            "subscriptions_total",
            "Total subscriptions",
            "plan", "status");

        _monthlyRevenue = _metrics.CreateGauge(
            "monthly_revenue_dollars",
            "Monthly recurring revenue");

        _featureUsage = _metrics.CreateCounter(
            "feature_usage_total",
            "Feature usage count",
            "feature", "user_type");

        _userSessionDuration = _metrics.CreateHistogram(
            "user_session_duration_minutes",
            "User session durations in minutes",
            new[] { 1, 5, 15, 30, 60, 120, 300 });
    }

    public void TrackUserRegistration(string source, string plan)
    {
        _userRegistrations.WithTags("source", source, "plan", plan).Inc();
    }

    public void TrackSubscription(string plan, string status)
    {
        _subscriptions.WithTags("plan", plan, "status", status).Inc();
    }

    public void UpdateMonthlyRevenue(decimal revenue)
    {
        _monthlyRevenue.Set((double)revenue);
    }

    public void TrackFeatureUsage(string feature, string userType)
    {
        _featureUsage.WithTags("feature", feature, "user_type", userType).Inc();
    }

    public void TrackUserSession(TimeSpan duration)
    {
        _userSessionDuration.Observe(duration.TotalMinutes);
    }
}
```

### 4. Application Performance Metrics

Monitor application performance:

```csharp
public class PerformanceMetricsService
{
    private readonly IMetrics _metrics;
    private readonly Histogram _databaseQueryDuration;
    private readonly Counter _databaseConnections;
    private readonly Gauge _memoryUsage;
    private readonly Counter _cacheHits;
    private readonly Histogram _externalApiCalls;

    public PerformanceMetricsService(IMetrics metrics)
    {
        _metrics = metrics;

        _databaseQueryDuration = _metrics.CreateHistogram(
            "database_query_duration_seconds",
            "Database query execution time",
            new[] { 0.001, 0.005, 0.01, 0.05, 0.1, 0.5, 1.0 },
            "operation", "table");

        _databaseConnections = _metrics.CreateCounter(
            "database_connections_total",
            "Database connection attempts",
            "status");

        _memoryUsage = _metrics.CreateGauge(
            "memory_usage_bytes",
            "Current memory usage in bytes");

        _cacheHits = _metrics.CreateCounter(
            "cache_operations_total",
            "Cache operations",
            "operation", "result");

        _externalApiCalls = _metrics.CreateHistogram(
            "external_api_call_duration_seconds",
            "External API call duration",
            new[] { 0.1, 0.5, 1.0, 2.0, 5.0, 10.0 },
            "service", "endpoint", "status");
    }

    public IDisposable TrackDatabaseQuery(string operation, string table)
    {
        return _databaseQueryDuration.WithTags("operation", operation, "table", table).NewTimer();
    }

    public void TrackDatabaseConnection(bool success)
    {
        _databaseConnections.WithTags("status", success ? "success" : "failure").Inc();
    }

    public void UpdateMemoryUsage(long bytes)
    {
        _memoryUsage.Set(bytes);
    }

    public void TrackCacheOperation(string operation, bool hit)
    {
        _cacheHits.WithTags("operation", operation, "result", hit ? "hit" : "miss").Inc();
    }

    public IDisposable TrackExternalApiCall(string service, string endpoint)
    {
        return _externalApiCalls.WithTags("service", service, "endpoint", endpoint, "status", "unknown").NewTimer();
    }
}

// Usage in repository
public class UserRepository
{
    private readonly PerformanceMetricsService _performanceMetrics;
    private readonly IDbContext _context;

    public async Task<User> GetByIdAsync(Guid id)
    {
        using var timer = _performanceMetrics.TrackDatabaseQuery("select", "users");

        try
        {
            var user = await _context.Users.FindAsync(id);
            _performanceMetrics.TrackDatabaseConnection(true);
            return user;
        }
        catch (Exception)
        {
            _performanceMetrics.TrackDatabaseConnection(false);
            throw;
        }
    }
}
```

### 5. Metrics Decorators

Use decorators to automatically add metrics to handlers:

```csharp
// Command handler metrics decorator
public class MetricsCommandHandlerDecorator<TCommand> : ICommandHandler<TCommand>
    where TCommand : class, ICommand
{
    private readonly ICommandHandler<TCommand> _handler;
    private readonly IMetrics _metrics;
    private readonly Counter _commandsProcessed;
    private readonly Histogram _commandDuration;

    public MetricsCommandHandlerDecorator(ICommandHandler<TCommand> handler, IMetrics metrics)
    {
        _handler = handler;
        _metrics = metrics;

        var commandName = typeof(TCommand).Name;

        _commandsProcessed = _metrics.CreateCounter(
            "commands_processed_total",
            "Total commands processed",
            "command", "status");

        _commandDuration = _metrics.CreateHistogram(
            "command_duration_seconds",
            "Command processing duration",
            new[] { 0.01, 0.05, 0.1, 0.5, 1.0, 2.0, 5.0 },
            "command");
    }

    public async Task HandleAsync(TCommand command, CancellationToken cancellationToken = default)
    {
        var commandName = typeof(TCommand).Name;

        using var timer = _commandDuration.WithTags("command", commandName).NewTimer();

        try
        {
            await _handler.HandleAsync(command, cancellationToken);
            _commandsProcessed.WithTags("command", commandName, "status", "success").Inc();
        }
        catch (ValidationException)
        {
            _commandsProcessed.WithTags("command", commandName, "status", "validation_error").Inc();
            throw;
        }
        catch (Exception)
        {
            _commandsProcessed.WithTags("command", commandName, "status", "error").Inc();
            throw;
        }
    }
}

// Query handler metrics decorator
public class MetricsQueryHandlerDecorator<TQuery, TResult> : IQueryHandler<TQuery, TResult>
    where TQuery : class, IQuery<TResult>
{
    private readonly IQueryHandler<TQuery, TResult> _handler;
    private readonly IMetrics _metrics;
    private readonly Counter _queriesProcessed;
    private readonly Histogram _queryDuration;

    public MetricsQueryHandlerDecorator(IQueryHandler<TQuery, TResult> handler, IMetrics metrics)
    {
        _handler = handler;
        _metrics = metrics;

        _queriesProcessed = _metrics.CreateCounter(
            "queries_processed_total",
            "Total queries processed",
            "query", "status");

        _queryDuration = _metrics.CreateHistogram(
            "query_duration_seconds",
            "Query processing duration",
            new[] { 0.001, 0.005, 0.01, 0.05, 0.1, 0.5, 1.0 },
            "query");
    }

    public async Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken = default)
    {
        var queryName = typeof(TQuery).Name;

        using var timer = _queryDuration.WithTags("query", queryName).NewTimer();

        try
        {
            var result = await _handler.HandleAsync(query, cancellationToken);
            _queriesProcessed.WithTags("query", queryName, "status", "success").Inc();
            return result;
        }
        catch (Exception)
        {
            _queriesProcessed.WithTags("query", queryName, "status", "error").Inc();
            throw;
        }
    }
}

// Registration
builder.Services.TryDecorate(typeof(ICommandHandler<>), typeof(MetricsCommandHandlerDecorator<>));
builder.Services.TryDecorate(typeof(IQueryHandler<,>), typeof(MetricsQueryHandlerDecorator<,>));
```

## Advanced Features

### 1. System Metrics Collection

Collect system-level metrics:

```csharp
public class SystemMetricsCollector : BackgroundService
{
    private readonly IMetrics _metrics;
    private readonly ILogger<SystemMetricsCollector> _logger;
    private readonly Gauge _cpuUsage;
    private readonly Gauge _memoryUsage;
    private readonly Gauge _diskUsage;
    private readonly Gauge _activeConnections;

    public SystemMetricsCollector(IMetrics metrics, ILogger<SystemMetricsCollector> logger)
    {
        _metrics = metrics;
        _logger = logger;

        _cpuUsage = _metrics.CreateGauge("system_cpu_usage_percent", "CPU usage percentage");
        _memoryUsage = _metrics.CreateGauge("system_memory_usage_bytes", "Memory usage in bytes");
        _diskUsage = _metrics.CreateGauge("system_disk_usage_percent", "Disk usage percentage");
        _activeConnections = _metrics.CreateGauge("system_active_connections", "Active network connections");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CollectSystemMetrics();
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting system metrics");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }

    private async Task CollectSystemMetrics()
    {
        // CPU usage
        var cpuUsage = await GetCpuUsageAsync();
        _cpuUsage.Set(cpuUsage);

        // Memory usage
        var process = Process.GetCurrentProcess();
        _memoryUsage.Set(process.WorkingSet64);

        // Disk usage
        var driveInfo = new DriveInfo(Path.GetPathRoot(Environment.CurrentDirectory));
        var diskUsagePercent = (double)(driveInfo.TotalSize - driveInfo.AvailableFreeSpace) / driveInfo.TotalSize * 100;
        _diskUsage.Set(diskUsagePercent);

        // Active connections (simplified)
        var connectionCount = GetActiveConnectionCount();
        _activeConnections.Set(connectionCount);
    }

    private async Task<double> GetCpuUsageAsync()
    {
        var startTime = DateTime.UtcNow;
        var startCpuUsage = Process.GetCurrentProcess().TotalProcessorTime;

        await Task.Delay(100);

        var endTime = DateTime.UtcNow;
        var endCpuUsage = Process.GetCurrentProcess().TotalProcessorTime;

        var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
        var totalMsPassed = (endTime - startTime).TotalMilliseconds;
        var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);

        return cpuUsageTotal * 100;
    }

    private int GetActiveConnectionCount()
    {
        // Implement connection counting logic
        return 0; // Placeholder
    }
}

// Registration
builder.Services.AddHostedService<SystemMetricsCollector>();
```

### 2. Custom Metrics Middleware

Create custom middleware for specific metrics:

```csharp
public class BusinessMetricsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IMetrics _metrics;
    private readonly Counter _apiUsage;
    private readonly Gauge _concurrentUsers;
    private readonly Histogram _responseSize;

    public BusinessMetricsMiddleware(RequestDelegate next, IMetrics metrics)
    {
        _next = next;
        _metrics = metrics;

        _apiUsage = _metrics.CreateCounter(
            "api_usage_total",
            "API endpoint usage",
            "endpoint", "method", "user_type");

        _concurrentUsers = _metrics.CreateGauge(
            "concurrent_users",
            "Number of concurrent users");

        _responseSize = _metrics.CreateHistogram(
            "response_size_bytes",
            "HTTP response size in bytes",
            new[] { 100, 1000, 10000, 100000, 1000000 });
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var originalBodyStream = context.Response.Body;
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        try
        {
            await _next(context);

            // Track API usage
            var endpoint = context.Request.Path.Value;
            var method = context.Request.Method;
            var userType = GetUserType(context);

            _apiUsage.WithTags("endpoint", endpoint, "method", method, "user_type", userType).Inc();

            // Track response size
            _responseSize.Observe(responseBody.Length);

            // Update concurrent users (simplified)
            UpdateConcurrentUsers(context);
        }
        finally
        {
            responseBody.Seek(0, SeekOrigin.Begin);
            await responseBody.CopyToAsync(originalBodyStream);
        }
    }

    private string GetUserType(HttpContext context)
    {
        if (!context.User.Identity.IsAuthenticated)
            return "anonymous";

        return context.User.IsInRole("Premium") ? "premium" : "standard";
    }

    private void UpdateConcurrentUsers(HttpContext context)
    {
        // Implement concurrent user tracking logic
        // This is a simplified example
        var sessionId = context.Session?.Id;
        if (!string.IsNullOrEmpty(sessionId))
        {
            // Track unique sessions
            // _concurrentUsers.Set(uniqueSessionCount);
        }
    }
}

// Registration
app.UseMiddleware<BusinessMetricsMiddleware>();
```

## Grafana Dashboard Configuration

### Sample Dashboard JSON

```json
{
  "dashboard": {
    "title": "Application Metrics",
    "panels": [
      {
        "title": "HTTP Request Rate",
        "type": "graph",
        "targets": [
          {
            "expr": "rate(http_requests_total[5m])",
            "legendFormat": "{{method}} {{status_code}}"
          }
        ]
      },
      {
        "title": "HTTP Request Duration",
        "type": "graph",
        "targets": [
          {
            "expr": "histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[5m]))",
            "legendFormat": "95th percentile"
          },
          {
            "expr": "histogram_quantile(0.50, rate(http_request_duration_seconds_bucket[5m]))",
            "legendFormat": "50th percentile"
          }
        ]
      },
      {
        "title": "Orders Processed",
        "type": "stat",
        "targets": [
          {
            "expr": "increase(orders_processed_total[1h])",
            "legendFormat": "Orders/hour"
          }
        ]
      },
      {
        "title": "Active Orders",
        "type": "graph",
        "targets": [
          {
            "expr": "active_orders",
            "legendFormat": "Active Orders"
          }
        ]
      },
      {
        "title": "System Resources",
        "type": "graph",
        "targets": [
          {
            "expr": "system_cpu_usage_percent",
            "legendFormat": "CPU %"
          },
          {
            "expr": "system_memory_usage_bytes / 1024 / 1024",
            "legendFormat": "Memory MB"
          }
        ]
      }
    ]
  }
}
```

## API Reference

### IMetrics Interface

```csharp
public interface IMetrics
{
    // Counter methods
    Counter CreateCounter(string name, string help, params string[] labelNames);

    // Gauge methods
    Gauge CreateGauge(string name, string help, params string[] labelNames);

    // Histogram methods
    Histogram CreateHistogram(string name, string help, double[] buckets = null, params string[] labelNames);

    // Summary methods
    Summary CreateSummary(string name, string help, double[] quantiles = null, params string[] labelNames);
}
```

### Metric Types

```csharp
// Counter - monotonically increasing
public interface Counter
{
    void Inc(double value = 1);
    Counter WithTags(params string[] labelValues);
}

// Gauge - can increase/decrease
public interface Gauge
{
    void Set(double value);
    void Inc(double value = 1);
    void Dec(double value = 1);
    Gauge WithTags(params string[] labelValues);
}

// Histogram - distribution of values
public interface Histogram
{
    void Observe(double value);
    ITimer NewTimer();
    Histogram WithTags(params string[] labelValues);
}

// Summary - distribution with quantiles
public interface Summary
{
    void Observe(double value);
    Summary WithTags(params string[] labelValues);
}
```

### Extension Methods

```csharp
public static class ConveyExtensions
{
    public static IConveyBuilder AddMetrics(this IConveyBuilder builder, string sectionName = "metrics");
}
```

## Best Practices

1. **Use appropriate metric types** - Counters for totals, gauges for current values, histograms for distributions
2. **Include relevant labels** - Add meaningful dimensions but avoid high cardinality
3. **Monitor business metrics** - Track business KPIs alongside technical metrics
4. **Use consistent naming** - Follow Prometheus naming conventions
5. **Implement proper tagging** - Use tags to segment metrics by relevant dimensions
6. **Set up alerting** - Configure alerts based on metric thresholds
7. **Monitor performance impact** - Ensure metrics collection doesn't impact application performance
8. **Document metrics** - Provide clear descriptions for all custom metrics

## Troubleshooting

### Common Issues

1. **Metrics not appearing**
   - Check if metrics endpoint is enabled
   - Verify Prometheus configuration
   - Ensure metrics are being created and updated

2. **High cardinality warnings**
   - Review label usage and reduce unnecessary labels
   - Use bounded label values
   - Consider using histograms instead of many gauges

3. **Performance impact**
   - Use appropriate sampling rates for high-volume metrics
   - Consider asynchronous metric collection
   - Monitor memory usage of metric storage

4. **Missing labels**
   - Ensure all label values are provided when creating metrics
   - Use default values for optional labels
   - Validate label consistency across metric updates
