---
layout: default
title: Convey.Metrics.AppMetrics
parent: Observability
---
# Convey.Metrics.AppMetrics

Application metrics collection and monitoring integration using App.Metrics framework providing comprehensive performance monitoring, custom metrics, and observability for microservices.

## Installation

```bash
dotnet add package Convey.Metrics.AppMetrics
```

## Overview

Convey.Metrics.AppMetrics provides:
- **Performance metrics** - Request/response times, throughput, and error rates
- **Custom metrics** - Counters, gauges, histograms, and timers
- **Health checks** - Application and dependency health monitoring
- **Multiple outputs** - Export to InfluxDB, Prometheus, Console, and more
- **CQRS integration** - Automatic metrics for commands, queries, and events
- **HTTP metrics** - Request tracking and performance monitoring
- **Business metrics** - Custom business KPIs and domain-specific metrics
- **Real-time monitoring** - Live metrics collection and reporting

## Configuration

### Basic Metrics Setup

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddMetrics();

var app = builder.Build();
app.UseMetrics();
app.Run();
```

### Advanced Configuration with Multiple Outputs

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddMetrics(metrics =>
    {
        metrics.Enabled = true;
        metrics.InfluxEnabled = true;
        metrics.PrometheusEnabled = true;
        metrics.PrometheusFormatter = true;
        metrics.InfluxUrl = "http://influxdb:8086";
        metrics.Database = "metrics";
        metrics.Env = "production";
        metrics.Interval = 5;
        metrics.Tags = new Dictionary<string, string>
        {
            ["service"] = "user-service",
            ["version"] = "1.0.0",
            ["environment"] = "production"
        };
    });

// Add custom metrics
builder.Services.AddSingleton<ICustomMetrics, CustomMetrics>();

var app = builder.Build();
app.UseMetrics();
app.UseMetricsTextEndpoint(); // Expose metrics at /metrics-text
app.UseMetricsAllEndpoints(); // Expose all metrics endpoints
app.Run();
```

### InfluxDB Configuration

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddMetrics(metrics =>
    {
        metrics.Enabled = true;
        metrics.InfluxEnabled = true;
        metrics.InfluxUrl = "http://influxdb:8086";
        metrics.Database = "microservices_metrics";
        metrics.Username = "admin";
        metrics.Password = "password";
        metrics.RetentionPolicy = "default";
        metrics.Env = "production";
        metrics.Interval = 10; // Report every 10 seconds
        metrics.Tags = new Dictionary<string, string>
        {
            ["service"] = Environment.GetEnvironmentVariable("SERVICE_NAME") ?? "unknown-service",
            ["version"] = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0",
            ["environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development",
            ["datacenter"] = Environment.GetEnvironmentVariable("DATACENTER") ?? "dc1",
            ["node"] = Environment.MachineName
        };
    });

var app = builder.Build();
app.UseMetrics();
app.Run();
```

### Prometheus Configuration

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddMetrics(metrics =>
    {
        metrics.Enabled = true;
        metrics.PrometheusEnabled = true;
        metrics.PrometheusFormatter = true;
        metrics.Interval = 15;
        metrics.Tags = new Dictionary<string, string>
        {
            ["job"] = "user-service",
            ["instance"] = $"{Environment.MachineName}:5000",
            ["environment"] = "production"
        };
    });

var app = builder.Build();
app.UseMetrics();
app.UseMetricsTextEndpoint("/metrics"); // Prometheus scraping endpoint
app.Run();
```

## Key Features

### 1. HTTP Request Metrics

Automatic HTTP request tracking and performance monitoring:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddWebApi()
    .AddMetrics();

var app = builder.Build();

app.UseMetrics();

// Automatic HTTP metrics collection
app.MapGet("/api/users", async (IUserService userService) =>
{
    // Request automatically tracked:
    // - Response time
    // - Status codes
    // - Request count
    // - Error rates
    var users = await userService.GetUsersAsync();
    return Results.Ok(users);
});

app.MapPost("/api/users", async (CreateUserRequest request, IUserService userService) =>
{
    var user = await userService.CreateUserAsync(request);
    return Results.Created($"/api/users/{user.Id}", user);
});

app.MapGet("/api/users/{id:guid}", async (Guid id, IUserService userService) =>
{
    var user = await userService.GetUserByIdAsync(id);
    return user != null ? Results.Ok(user) : Results.NotFound();
});

app.MapPut("/api/users/{id:guid}", async (Guid id, UpdateUserRequest request, IUserService userService) =>
{
    await userService.UpdateUserAsync(id, request);
    return Results.NoContent();
});

app.MapDelete("/api/users/{id:guid}", async (Guid id, IUserService userService) =>
{
    await userService.DeleteUserAsync(id);
    return Results.NoContent();
});

app.Run();
```

### 2. CQRS Metrics Integration

Automatic metrics collection for CQRS operations:

```csharp
// Commands with automatic metrics
public class CreateUser : ICommand
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public UserRole Role { get; set; }
}

public class UpdateUser : ICommand
{
    public Guid Id { get; set; }
    public string Email { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public bool IsActive { get; set; }
}

public class DeleteUser : ICommand
{
    public Guid Id { get; set; }
    public bool SoftDelete { get; set; } = true;
}

// Command handlers with automatic metrics collection
public class CreateUserHandler : ICommandHandler<CreateUser>
{
    private readonly IUserRepository _userRepository;
    private readonly IEventBus _eventBus;
    private readonly ICustomMetrics _customMetrics;

    public CreateUserHandler(
        IUserRepository userRepository,
        IEventBus eventBus,
        ICustomMetrics customMetrics)
    {
        _userRepository = userRepository;
        _eventBus = eventBus;
        _customMetrics = customMetrics;
    }

    // Automatically tracked metrics:
    // - Command execution time
    // - Success/failure rates
    // - Command throughput
    public async Task HandleAsync(CreateUser command, CancellationToken cancellationToken = default)
    {
        // Custom business metric
        _customMetrics.IncrementUserRegistrations(command.Role);

        var existingUser = await _userRepository.GetByEmailAsync(command.Email);
        if (existingUser != null)
        {
            _customMetrics.IncrementDuplicateEmailAttempts();
            throw new UserAlreadyExistsException(command.Email);
        }

        var user = new User
        {
            Id = command.Id,
            Email = command.Email,
            FirstName = command.FirstName,
            LastName = command.LastName,
            Role = command.Role,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        await _userRepository.CreateAsync(user);

        var userCreatedEvent = new UserCreated
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role,
            CreatedAt = user.CreatedAt
        };

        await _eventBus.PublishAsync(userCreatedEvent, cancellationToken);

        // Track successful user creation
        _customMetrics.IncrementSuccessfulUserCreations(command.Role);
    }
}

// Queries with automatic metrics
public class GetUsers : IQuery<PagedResult<UserDto>>
{
    public string SearchTerm { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public UserRole? Role { get; set; }
    public bool? IsActive { get; set; } = true;
}

public class GetUsersHandler : IQueryHandler<GetUsers, PagedResult<UserDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly ICustomMetrics _customMetrics;

    public GetUsersHandler(IUserRepository userRepository, ICustomMetrics customMetrics)
    {
        _userRepository = userRepository;
        _customMetrics = customMetrics;
    }

    // Automatically tracked:
    // - Query execution time
    // - Result set sizes
    // - Query patterns
    public async Task<PagedResult<UserDto>> HandleAsync(GetUsers query, CancellationToken cancellationToken = default)
    {
        // Track query patterns
        _customMetrics.RecordUserQuery(query.SearchTerm, query.Role, query.Page, query.PageSize);

        var users = await _userRepository.GetUsersAsync(
            searchTerm: query.SearchTerm,
            page: query.Page,
            pageSize: query.PageSize,
            role: query.Role,
            isActive: query.IsActive);

        var totalCount = await _userRepository.GetUserCountAsync(
            searchTerm: query.SearchTerm,
            role: query.Role,
            isActive: query.IsActive);

        var userDtos = users.Select(u => new UserDto
        {
            Id = u.Id,
            Email = u.Email,
            FirstName = u.FirstName,
            LastName = u.LastName,
            Role = u.Role,
            IsActive = u.IsActive,
            CreatedAt = u.CreatedAt,
            UpdatedAt = u.UpdatedAt
        }).ToList();

        var result = new PagedResult<UserDto>
        {
            Items = userDtos,
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalPages = (int)Math.Ceiling((double)totalCount / query.PageSize)
        };

        // Record result metrics
        _customMetrics.RecordQueryResults(result.Items.Count, result.TotalCount);

        return result;
    }
}

// Events with automatic metrics
public class UserCreated : IEvent
{
    public Guid Id { get; set; }
    public string Email { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public UserRole Role { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UserCreatedHandler : IEventHandler<UserCreated>
{
    private readonly IEmailService _emailService;
    private readonly ICustomMetrics _customMetrics;

    public UserCreatedHandler(IEmailService emailService, ICustomMetrics customMetrics)
    {
        _emailService = emailService;
        _customMetrics = customMetrics;
    }

    // Event handling metrics automatically tracked
    public async Task HandleAsync(UserCreated @event, CancellationToken cancellationToken = default)
    {
        try
        {
            await _emailService.SendWelcomeEmailAsync(new WelcomeEmailRequest
            {
                RecipientEmail = @event.Email,
                FirstName = @event.FirstName,
                LastName = @event.LastName,
                UserId = @event.Id
            });

            _customMetrics.IncrementWelcomeEmailsSent(@event.Role);
        }
        catch (Exception ex)
        {
            _customMetrics.IncrementWelcomeEmailFailures(@event.Role);
            throw;
        }
    }
}
```

### 3. Custom Business Metrics

Implementing custom metrics for business KPIs:

```csharp
// Custom metrics service
public interface ICustomMetrics
{
    void IncrementUserRegistrations(UserRole role);
    void IncrementSuccessfulUserCreations(UserRole role);
    void IncrementDuplicateEmailAttempts();
    void IncrementWelcomeEmailsSent(UserRole role);
    void IncrementWelcomeEmailFailures(UserRole role);
    void RecordUserQuery(string searchTerm, UserRole? role, int page, int pageSize);
    void RecordQueryResults(int resultCount, int totalCount);
    void RecordDatabaseOperationTime(string operation, double milliseconds);
    void RecordCacheHitRate(bool isHit, string cacheType);
    void SetActiveUserCount(int count);
    void RecordBusinessTransaction(string transactionType, decimal amount);
    void IncrementApiCallsPerCustomer(string customerId);
}

public class CustomMetrics : ICustomMetrics
{
    private readonly IMetrics _metrics;

    public CustomMetrics(IMetrics metrics)
    {
        _metrics = metrics;
    }

    public void IncrementUserRegistrations(UserRole role)
    {
        _metrics.Measure.Counter.Increment(
            MetricRegistry.Counters.UserRegistrations,
            new MetricTags("role", role.ToString().ToLower()));
    }

    public void IncrementSuccessfulUserCreations(UserRole role)
    {
        _metrics.Measure.Counter.Increment(
            MetricRegistry.Counters.SuccessfulUserCreations,
            new MetricTags("role", role.ToString().ToLower()));
    }

    public void IncrementDuplicateEmailAttempts()
    {
        _metrics.Measure.Counter.Increment(MetricRegistry.Counters.DuplicateEmailAttempts);
    }

    public void IncrementWelcomeEmailsSent(UserRole role)
    {
        _metrics.Measure.Counter.Increment(
            MetricRegistry.Counters.WelcomeEmailsSent,
            new MetricTags("role", role.ToString().ToLower()));
    }

    public void IncrementWelcomeEmailFailures(UserRole role)
    {
        _metrics.Measure.Counter.Increment(
            MetricRegistry.Counters.WelcomeEmailFailures,
            new MetricTags("role", role.ToString().ToLower()));
    }

    public void RecordUserQuery(string searchTerm, UserRole? role, int page, int pageSize)
    {
        var tags = new List<KeyValuePair<string, string>>
        {
            new("has_search", (!string.IsNullOrEmpty(searchTerm)).ToString().ToLower()),
            new("page_size", pageSize.ToString())
        };

        if (role.HasValue)
        {
            tags.Add(new("role", role.ToString().ToLower()));
        }

        _metrics.Measure.Counter.Increment(
            MetricRegistry.Counters.UserQueries,
            new MetricTags(tags.ToArray()));

        // Record page number distribution
        _metrics.Measure.Histogram.Update(
            MetricRegistry.Histograms.QueryPageNumbers,
            page);
    }

    public void RecordQueryResults(int resultCount, int totalCount)
    {
        _metrics.Measure.Histogram.Update(MetricRegistry.Histograms.QueryResultCounts, resultCount);
        _metrics.Measure.Histogram.Update(MetricRegistry.Histograms.QueryTotalCounts, totalCount);
    }

    public void RecordDatabaseOperationTime(string operation, double milliseconds)
    {
        _metrics.Measure.Timer.Time(
            MetricRegistry.Timers.DatabaseOperations,
            TimeSpan.FromMilliseconds(milliseconds),
            new MetricTags("operation", operation.ToLower()));
    }

    public void RecordCacheHitRate(bool isHit, string cacheType)
    {
        _metrics.Measure.Counter.Increment(
            isHit ? MetricRegistry.Counters.CacheHits : MetricRegistry.Counters.CacheMisses,
            new MetricTags("type", cacheType.ToLower()));
    }

    public void SetActiveUserCount(int count)
    {
        _metrics.Measure.Gauge.SetValue(MetricRegistry.Gauges.ActiveUsers, count);
    }

    public void RecordBusinessTransaction(string transactionType, decimal amount)
    {
        _metrics.Measure.Counter.Increment(
            MetricRegistry.Counters.BusinessTransactions,
            new MetricTags("type", transactionType.ToLower()));

        _metrics.Measure.Histogram.Update(
            MetricRegistry.Histograms.TransactionAmounts,
            (double)amount,
            new MetricTags("type", transactionType.ToLower()));
    }

    public void IncrementApiCallsPerCustomer(string customerId)
    {
        _metrics.Measure.Counter.Increment(
            MetricRegistry.Counters.ApiCallsPerCustomer,
            new MetricTags("customer_id", customerId));
    }
}

// Metric registry for organized metric definitions
public static class MetricRegistry
{
    public static class Counters
    {
        public static readonly CounterOptions UserRegistrations = new CounterOptions
        {
            Name = "user_registrations_total",
            MeasurementUnit = Unit.Calls,
            Tags = new MetricTags("role", "unknown")
        };

        public static readonly CounterOptions SuccessfulUserCreations = new CounterOptions
        {
            Name = "successful_user_creations_total",
            MeasurementUnit = Unit.Calls,
            Tags = new MetricTags("role", "unknown")
        };

        public static readonly CounterOptions DuplicateEmailAttempts = new CounterOptions
        {
            Name = "duplicate_email_attempts_total",
            MeasurementUnit = Unit.Calls
        };

        public static readonly CounterOptions WelcomeEmailsSent = new CounterOptions
        {
            Name = "welcome_emails_sent_total",
            MeasurementUnit = Unit.Calls,
            Tags = new MetricTags("role", "unknown")
        };

        public static readonly CounterOptions WelcomeEmailFailures = new CounterOptions
        {
            Name = "welcome_email_failures_total",
            MeasurementUnit = Unit.Calls,
            Tags = new MetricTags("role", "unknown")
        };

        public static readonly CounterOptions UserQueries = new CounterOptions
        {
            Name = "user_queries_total",
            MeasurementUnit = Unit.Calls
        };

        public static readonly CounterOptions CacheHits = new CounterOptions
        {
            Name = "cache_hits_total",
            MeasurementUnit = Unit.Calls,
            Tags = new MetricTags("type", "unknown")
        };

        public static readonly CounterOptions CacheMisses = new CounterOptions
        {
            Name = "cache_misses_total",
            MeasurementUnit = Unit.Calls,
            Tags = new MetricTags("type", "unknown")
        };

        public static readonly CounterOptions BusinessTransactions = new CounterOptions
        {
            Name = "business_transactions_total",
            MeasurementUnit = Unit.Calls,
            Tags = new MetricTags("type", "unknown")
        };

        public static readonly CounterOptions ApiCallsPerCustomer = new CounterOptions
        {
            Name = "api_calls_per_customer_total",
            MeasurementUnit = Unit.Calls,
            Tags = new MetricTags("customer_id", "unknown")
        };
    }

    public static class Gauges
    {
        public static readonly GaugeOptions ActiveUsers = new GaugeOptions
        {
            Name = "active_users_current",
            MeasurementUnit = Unit.Items
        };

        public static readonly GaugeOptions DatabaseConnections = new GaugeOptions
        {
            Name = "database_connections_current",
            MeasurementUnit = Unit.Connections
        };

        public static readonly GaugeOptions CacheSize = new GaugeOptions
        {
            Name = "cache_size_bytes",
            MeasurementUnit = Unit.Bytes,
            Tags = new MetricTags("type", "unknown")
        };
    }

    public static class Histograms
    {
        public static readonly HistogramOptions QueryResultCounts = new HistogramOptions
        {
            Name = "query_result_counts",
            MeasurementUnit = Unit.Items
        };

        public static readonly HistogramOptions QueryTotalCounts = new HistogramOptions
        {
            Name = "query_total_counts",
            MeasurementUnit = Unit.Items
        };

        public static readonly HistogramOptions QueryPageNumbers = new HistogramOptions
        {
            Name = "query_page_numbers",
            MeasurementUnit = Unit.None
        };

        public static readonly HistogramOptions TransactionAmounts = new HistogramOptions
        {
            Name = "transaction_amounts",
            MeasurementUnit = Unit.None,
            Tags = new MetricTags("type", "unknown")
        };
    }

    public static class Timers
    {
        public static readonly TimerOptions DatabaseOperations = new TimerOptions
        {
            Name = "database_operation_duration",
            MeasurementUnit = Unit.Requests,
            DurationUnit = TimeUnit.Milliseconds,
            RateUnit = TimeUnit.Seconds,
            Tags = new MetricTags("operation", "unknown")
        };

        public static readonly TimerOptions ExternalApiCalls = new TimerOptions
        {
            Name = "external_api_call_duration",
            MeasurementUnit = Unit.Requests,
            DurationUnit = TimeUnit.Milliseconds,
            RateUnit = TimeUnit.Seconds,
            Tags = new MetricTags("service", "unknown")
        };
    }
}
```

### 4. Health Checks Integration

Combining metrics with health checks:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database")
    .AddCheck<RedisHealthCheck>("redis")
    .AddCheck<EmailServiceHealthCheck>("email-service")
    .AddCheck<ExternalApiHealthCheck>("external-api");

builder.Services.AddConvey()
    .AddMetrics(metrics =>
    {
        metrics.Enabled = true;
        metrics.InfluxEnabled = true;
        metrics.InfluxUrl = "http://influxdb:8086";
        metrics.Database = "health_metrics";
        metrics.Interval = 30;
    });

var app = builder.Build();

// Health check endpoints
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});

app.UseMetrics();
app.Run();

// Custom health checks with metrics
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly IDbContext _dbContext;
    private readonly ICustomMetrics _customMetrics;

    public DatabaseHealthCheck(IDbContext dbContext, ICustomMetrics customMetrics)
    {
        _dbContext = dbContext;
        _customMetrics = customMetrics;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var stopwatch = Stopwatch.StartNew();
            await _dbContext.Database.CanConnectAsync(cancellationToken);
            stopwatch.Stop();

            _customMetrics.RecordDatabaseOperationTime("health_check", stopwatch.ElapsedMilliseconds);

            return HealthCheckResult.Healthy("Database connection successful",
                new Dictionary<string, object>
                {
                    ["response_time_ms"] = stopwatch.ElapsedMilliseconds
                });
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database connection failed", ex);
        }
    }
}

public class RedisHealthCheck : IHealthCheck
{
    private readonly IRedisService _redisService;
    private readonly ICustomMetrics _customMetrics;

    public RedisHealthCheck(IRedisService redisService, ICustomMetrics customMetrics)
    {
        _redisService = redisService;
        _customMetrics = customMetrics;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var stopwatch = Stopwatch.StartNew();
            var testKey = $"health_check_{Guid.NewGuid()}";
            await _redisService.SetAsync(testKey, "test", TimeSpan.FromSeconds(5));
            var value = await _redisService.GetAsync<string>(testKey);
            await _redisService.DeleteAsync(testKey);
            stopwatch.Stop();

            if (value != "test")
            {
                return HealthCheckResult.Degraded("Redis read/write test failed");
            }

            _customMetrics.RecordCacheHitRate(true, "redis");

            return HealthCheckResult.Healthy("Redis connection successful",
                new Dictionary<string, object>
                {
                    ["response_time_ms"] = stopwatch.ElapsedMilliseconds
                });
        }
        catch (Exception ex)
        {
            _customMetrics.RecordCacheHitRate(false, "redis");
            return HealthCheckResult.Unhealthy("Redis connection failed", ex);
        }
    }
}
```

### 5. Performance Monitoring

Advanced performance monitoring and alerting:

```csharp
// Performance monitoring middleware
public class PerformanceMonitoringMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ICustomMetrics _customMetrics;
    private readonly ILogger<PerformanceMonitoringMiddleware> _logger;

    public PerformanceMonitoringMiddleware(
        RequestDelegate next,
        ICustomMetrics customMetrics,
        ILogger<PerformanceMonitoringMiddleware> logger)
    {
        _next = next;
        _customMetrics = customMetrics;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var originalBodyStream = context.Response.Body;

        try
        {
            using var responseBody = new MemoryStream();
            context.Response.Body = responseBody;

            await _next(context);

            stopwatch.Stop();

            // Record metrics
            var endpoint = context.Request.Path.Value ?? "unknown";
            var method = context.Request.Method;
            var statusCode = context.Response.StatusCode;
            var responseTime = stopwatch.ElapsedMilliseconds;

            // Record response time
            _customMetrics.RecordHttpRequestDuration(method, endpoint, statusCode, responseTime);

            // Record response size
            var responseSize = responseBody.Length;
            _customMetrics.RecordHttpResponseSize(method, endpoint, responseSize);

            // Copy response back
            await responseBody.CopyToAsync(originalBodyStream);

            // Log slow requests
            if (responseTime > 1000) // Slow request threshold
            {
                _logger.LogWarning("Slow request detected: {Method} {Path} took {Duration}ms",
                    method, endpoint, responseTime);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _customMetrics.RecordHttpRequestError(context.Request.Method, context.Request.Path.Value ?? "unknown", ex.GetType().Name);
            throw;
        }
        finally
        {
            context.Response.Body = originalBodyStream;
        }
    }
}

// Service for background metrics collection
public class MetricsCollectionService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MetricsCollectionService> _logger;

    public MetricsCollectionService(IServiceProvider serviceProvider, ILogger<MetricsCollectionService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var customMetrics = scope.ServiceProvider.GetRequiredService<ICustomMetrics>();
                var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();

                // Collect system metrics
                await CollectSystemMetrics(customMetrics);

                // Collect business metrics
                await CollectBusinessMetrics(customMetrics, userRepository);

                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting metrics");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }

    private async Task CollectSystemMetrics(ICustomMetrics customMetrics)
    {
        // System metrics
        var process = Process.GetCurrentProcess();
        customMetrics.SetProcessMemoryUsage(process.WorkingSet64);
        customMetrics.SetProcessCpuUsage(process.TotalProcessorTime.TotalMilliseconds);

        // GC metrics
        var gen0Collections = GC.CollectionCount(0);
        var gen1Collections = GC.CollectionCount(1);
        var gen2Collections = GC.CollectionCount(2);
        var totalMemory = GC.GetTotalMemory(false);

        customMetrics.SetGcCollections(0, gen0Collections);
        customMetrics.SetGcCollections(1, gen1Collections);
        customMetrics.SetGcCollections(2, gen2Collections);
        customMetrics.SetGcMemoryUsage(totalMemory);
    }

    private async Task CollectBusinessMetrics(ICustomMetrics customMetrics, IUserRepository userRepository)
    {
        // Active user count
        var activeUserCount = await userRepository.GetActiveUserCountAsync();
        customMetrics.SetActiveUserCount(activeUserCount);

        // User registrations today
        var today = DateTime.UtcNow.Date;
        var todayRegistrations = await userRepository.GetUserCountByDateAsync(today);
        customMetrics.SetDailyRegistrations(todayRegistrations);

        // User distribution by role
        var usersByRole = await userRepository.GetUserCountByRoleAsync();
        foreach (var roleCount in usersByRole)
        {
            customMetrics.SetUserCountByRole(roleCount.Key, roleCount.Value);
        }
    }
}

// Extended custom metrics interface
public interface IExtendedCustomMetrics : ICustomMetrics
{
    void RecordHttpRequestDuration(string method, string endpoint, int statusCode, double milliseconds);
    void RecordHttpResponseSize(string method, string endpoint, long bytes);
    void RecordHttpRequestError(string method, string endpoint, string errorType);
    void SetProcessMemoryUsage(long bytes);
    void SetProcessCpuUsage(double milliseconds);
    void SetGcCollections(int generation, int count);
    void SetGcMemoryUsage(long bytes);
    void SetDailyRegistrations(int count);
    void SetUserCountByRole(UserRole role, int count);
}
```

## Configuration Options

### Metrics Options

```csharp
public class MetricsOptions
{
    public bool Enabled { get; set; } = true;
    public bool InfluxEnabled { get; set; } = false;
    public bool PrometheusEnabled { get; set; } = false;
    public bool PrometheusFormatter { get; set; } = false;
    public string InfluxUrl { get; set; }
    public string Database { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public string RetentionPolicy { get; set; }
    public string Env { get; set; }
    public int Interval { get; set; } = 5;
    public Dictionary<string, string> Tags { get; set; } = new();
}
```

## API Reference

### Extension Methods

```csharp
public static class ConveyExtensions
{
    public static IConveyBuilder AddMetrics(this IConveyBuilder builder);
    public static IConveyBuilder AddMetrics(this IConveyBuilder builder, Action<MetricsOptions> configure);
}

public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseMetrics(this IApplicationBuilder app);
}
```

## Best Practices

1. **Use meaningful metric names** - Follow naming conventions for clarity
2. **Add appropriate tags** - Use tags for filtering and grouping
3. **Monitor performance impact** - Ensure metrics collection doesn't degrade performance
4. **Set up alerting** - Configure alerts for critical metrics
5. **Use appropriate metric types** - Choose counters, gauges, histograms, or timers appropriately
6. **Implement data retention** - Configure appropriate data retention policies
7. **Monitor metric cardinality** - Avoid high-cardinality metrics that can overwhelm systems
8. **Document metrics** - Maintain documentation for custom metrics and their purposes

## Troubleshooting

### Common Issues

1. **High cardinality metrics**
   - Review tag usage and avoid unbounded tags
   - Implement sampling for high-volume metrics
   - Use metric aggregation strategies

2. **Performance impact**
   - Monitor metrics collection overhead
   - Optimize metric calculation and reporting
   - Consider asynchronous metric collection

3. **Storage issues**
   - Configure appropriate retention policies
   - Monitor storage usage and capacity
   - Implement metric archival strategies

4. **Missing metrics**
   - Verify metric registration and configuration
   - Check metric collection intervals
   - Validate metric output endpoints

