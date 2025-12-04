---
layout: default
title: Convey.Logging
parent: Observability
---
# Convey.Logging

Structured logging abstractions and integrations providing consistent logging patterns, contextual enrichment, and seamless integration with popular logging frameworks like Serilog and NLog.

## Installation

```bash
dotnet add package Convey.Logging
```

## Overview

Convey.Logging provides:
- **Structured logging** - Consistent structured logging patterns
- **Context enrichment** - Automatic context injection (correlation ID, user info, etc.)
- **Framework integration** - Seamless integration with Serilog, NLog, and Microsoft.Extensions.Logging
- **Performance optimized** - High-performance logging with minimal overhead
- **Configuration support** - Flexible logging configuration options
- **Correlation tracking** - Built-in correlation ID support for distributed tracing

## Configuration

### Basic Setup

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddConsoleLogging(); // Basic console logging

var app = builder.Build();
```

### Serilog Integration

```csharp
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/app-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddConvey()
    .AddLogging(); // Uses configured Serilog

var app = builder.Build();
```

### Advanced Configuration

Configure in `appsettings.json`:

```json
{
  "logger": {
    "applicationName": "MyService",
    "excludePaths": ["/health", "/metrics"],
    "excludeRequestBody": false,
    "excludeResponseBody": false,
    "console": {
      "enabled": true
    },
    "file": {
      "enabled": true,
      "path": "logs/app.txt",
      "interval": "day"
    },
    "seq": {
      "enabled": true,
      "url": "http://localhost:5341",
      "apiKey": ""
    },
    "tags": {}
  }
}
```

## Key Features

### 1. Structured Logging

Use structured logging with contextual information:

```csharp
public class UserService
{
    private readonly ILogger<UserService> _logger;

    public UserService(ILogger<UserService> logger)
    {
        _logger = logger;
    }

    public async Task<User> GetUserAsync(Guid userId)
    {
        _logger.LogInformation("Getting user {UserId}", userId);

        try
        {
            var user = await _repository.GetAsync(userId);

            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found", userId);
                return null;
            }

            _logger.LogInformation("Successfully retrieved user {UserId} with email {Email}",
                userId, user.Email);

            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user {UserId}", userId);
            throw;
        }
    }

    public async Task CreateUserAsync(CreateUserCommand command)
    {
        using var scope = _logger.BeginScope("Creating user {Email}", command.Email);

        _logger.LogInformation("Starting user creation process");

        try
        {
            // Validate
            _logger.LogDebug("Validating user data");
            await ValidateUserAsync(command);

            // Create
            _logger.LogDebug("Creating user entity");
            var user = new User(command.Email, command.Name);

            // Save
            _logger.LogDebug("Saving user to repository");
            await _repository.AddAsync(user);

            _logger.LogInformation("User created successfully with ID {UserId}", user.Id);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning("User validation failed: {Errors}",
                string.Join(", ", ex.Errors.Select(e => e.ErrorMessage)));
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create user");
            throw;
        }
    }
}
```

### 2. Context Enrichment

Automatically enrich logs with contextual information:

```csharp
// Custom enrichers
public class CorrelationIdEnricher : ILogEventEnricher
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CorrelationIdEnricher(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var correlationId = _httpContextAccessor.HttpContext?.Items["CorrelationId"]?.ToString()
                           ?? Guid.NewGuid().ToString();

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("CorrelationId", correlationId));
    }
}

public class UserContextEnricher : ILogEventEnricher
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UserContextEnricher(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User?.Identity?.IsAuthenticated == true)
        {
            var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userName = httpContext.User.FindFirst(ClaimTypes.Name)?.Value;

            if (!string.IsNullOrEmpty(userId))
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("UserId", userId));
            }

            if (!string.IsNullOrEmpty(userName))
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("UserName", userName));
            }
        }
    }
}

// Registration
Log.Logger = new LoggerConfiguration()
    .Enrich.With<CorrelationIdEnricher>()
    .Enrich.With<UserContextEnricher>()
    .Enrich.WithProperty("Application", "MyService")
    .Enrich.WithProperty("Environment", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"))
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} " +
        "{CorrelationId} {UserId} {UserName} " +
        "{NewLine}{Exception}")
    .CreateLogger();
```

### 3. HTTP Request/Response Logging

Log HTTP requests and responses:

```csharp
// Middleware for request/response logging
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Generate correlation ID
        var correlationId = Guid.NewGuid().ToString();
        context.Items["CorrelationId"] = correlationId;
        context.Response.Headers["X-Correlation-ID"] = correlationId;

        // Log request
        await LogRequestAsync(context);

        // Capture response
        var originalBodyStream = context.Response.Body;
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();

            // Log response
            await LogResponseAsync(context, stopwatch.ElapsedMilliseconds);

            // Copy response back
            responseBody.Seek(0, SeekOrigin.Begin);
            await responseBody.CopyToAsync(originalBodyStream);
        }
    }

    private async Task LogRequestAsync(HttpContext context)
    {
        var request = context.Request;

        // Read request body
        string requestBody = null;
        if (request.ContentLength > 0 && ShouldLogBody(request.Path))
        {
            request.EnableBuffering();
            var buffer = new byte[Convert.ToInt32(request.ContentLength)];
            await request.Body.ReadAsync(buffer, 0, buffer.Length);
            requestBody = Encoding.UTF8.GetString(buffer);
            request.Body.Position = 0;
        }

        _logger.LogInformation("HTTP {Method} {Path}{Query} started",
            request.Method,
            request.Path,
            request.QueryString);

        if (!string.IsNullOrEmpty(requestBody))
        {
            _logger.LogDebug("Request body: {RequestBody}", requestBody);
        }
    }

    private async Task LogResponseAsync(HttpContext context, long elapsedMs)
    {
        var response = context.Response;

        // Read response body
        string responseBody = null;
        if (response.Body.Length > 0 && ShouldLogBody(context.Request.Path))
        {
            response.Body.Seek(0, SeekOrigin.Begin);
            responseBody = await new StreamReader(response.Body).ReadToEndAsync();
            response.Body.Seek(0, SeekOrigin.Begin);
        }

        var logLevel = response.StatusCode >= 400 ? LogLevel.Warning : LogLevel.Information;

        _logger.Log(logLevel, "HTTP {Method} {Path}{Query} responded {StatusCode} in {ElapsedMs}ms",
            context.Request.Method,
            context.Request.Path,
            context.Request.QueryString,
            response.StatusCode,
            elapsedMs);

        if (!string.IsNullOrEmpty(responseBody))
        {
            _logger.LogDebug("Response body: {ResponseBody}", responseBody);
        }
    }

    private bool ShouldLogBody(PathString path)
    {
        var excludePaths = new[] { "/health", "/metrics", "/swagger" };
        return !excludePaths.Any(p => path.StartsWithSegments(p));
    }
}

// Registration
app.UseMiddleware<RequestLoggingMiddleware>();
```

### 4. Performance Logging

Log performance metrics and slow operations:

```csharp
// Performance logging decorator
public class PerformanceLoggingDecorator<TCommand> : ICommandHandler<TCommand>
    where TCommand : class, ICommand
{
    private readonly ICommandHandler<TCommand> _handler;
    private readonly ILogger<PerformanceLoggingDecorator<TCommand>> _logger;

    public PerformanceLoggingDecorator(
        ICommandHandler<TCommand> handler,
        ILogger<PerformanceLoggingDecorator<TCommand>> logger)
    {
        _handler = handler;
        _logger = logger;
    }

    public async Task HandleAsync(TCommand command, CancellationToken cancellationToken = default)
    {
        var commandName = typeof(TCommand).Name;
        var stopwatch = Stopwatch.StartNew();

        _logger.LogDebug("Executing command {CommandName}", commandName);

        try
        {
            await _handler.HandleAsync(command, cancellationToken);

            stopwatch.Stop();

            var logLevel = stopwatch.ElapsedMilliseconds > 1000 ? LogLevel.Warning : LogLevel.Information;

            _logger.Log(logLevel, "Command {CommandName} executed in {ElapsedMs}ms",
                commandName, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(ex, "Command {CommandName} failed after {ElapsedMs}ms",
                commandName, stopwatch.ElapsedMilliseconds);

            throw;
        }
    }
}

// Usage with decorator pattern
builder.Services.TryDecorate(typeof(ICommandHandler<>), typeof(PerformanceLoggingDecorator<>));
```

## Advanced Features

### 1. Custom Log Levels

Define custom log levels for specific scenarios:

```csharp
public static class CustomLogLevels
{
    public static LogEventLevel Business = LogEventLevel.Information;
    public static LogEventLevel Security = LogEventLevel.Warning;
    public static LogEventLevel Performance = LogEventLevel.Information;
    public static LogEventLevel Integration = LogEventLevel.Information;
}

public static class LoggerExtensions
{
    public static void LogBusiness(this ILogger logger, string messageTemplate, params object[] propertyValues)
    {
        logger.LogInformation("BUSINESS: " + messageTemplate, propertyValues);
    }

    public static void LogSecurity(this ILogger logger, string messageTemplate, params object[] propertyValues)
    {
        logger.LogWarning("SECURITY: " + messageTemplate, propertyValues);
    }

    public static void LogPerformance(this ILogger logger, string messageTemplate, params object[] propertyValues)
    {
        logger.LogInformation("PERFORMANCE: " + messageTemplate, propertyValues);
    }

    public static void LogIntegration(this ILogger logger, string messageTemplate, params object[] propertyValues)
    {
        logger.LogInformation("INTEGRATION: " + messageTemplate, propertyValues);
    }
}

// Usage
public class OrderService
{
    private readonly ILogger<OrderService> _logger;

    public async Task ProcessOrderAsync(ProcessOrderCommand command)
    {
        _logger.LogBusiness("Processing order {OrderId} for customer {CustomerId}",
            command.OrderId, command.CustomerId);

        // Security logging
        if (command.Amount > 10000)
        {
            _logger.LogSecurity("High-value order {OrderId} for amount {Amount}",
                command.OrderId, command.Amount);
        }

        // Performance logging
        var stopwatch = Stopwatch.StartNew();
        await ProcessOrder(command);
        stopwatch.Stop();

        _logger.LogPerformance("Order {OrderId} processed in {ElapsedMs}ms",
            command.OrderId, stopwatch.ElapsedMilliseconds);

        // Integration logging
        _logger.LogIntegration("Sending order {OrderId} confirmation to payment service",
            command.OrderId);
    }
}
```

### 2. Scoped Properties

Use scoped properties for contextual logging:

```csharp
public class OrderProcessingService
{
    private readonly ILogger<OrderProcessingService> _logger;

    public async Task ProcessOrderAsync(Guid orderId)
    {
        using var orderScope = _logger.BeginScope("OrderId", orderId);

        _logger.LogInformation("Starting order processing");

        try
        {
            // Validate order
            using var validationScope = _logger.BeginScope("Step", "Validation");
            _logger.LogInformation("Validating order");
            await ValidateOrderAsync(orderId);

            // Process payment
            using var paymentScope = _logger.BeginScope("Step", "Payment");
            _logger.LogInformation("Processing payment");
            await ProcessPaymentAsync(orderId);

            // Update inventory
            using var inventoryScope = _logger.BeginScope("Step", "Inventory");
            _logger.LogInformation("Updating inventory");
            await UpdateInventoryAsync(orderId);

            _logger.LogInformation("Order processing completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Order processing failed");
            throw;
        }
    }
}
```

### 3. Conditional Logging

Implement conditional logging based on configuration:

```csharp
public class ConditionalLogger<T> : ILogger<T>
{
    private readonly ILogger<T> _logger;
    private readonly LoggerOptions _options;

    public ConditionalLogger(ILogger<T> logger, IOptions<LoggerOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public IDisposable BeginScope<TState>(TState state) => _logger.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel)
    {
        if (!_options.Enabled)
            return false;

        var categoryName = typeof(T).FullName;

        // Check if category is excluded
        if (_options.ExcludedCategories?.Contains(categoryName) == true)
            return false;

        // Check minimum log level for category
        if (_options.CategoryLevels?.TryGetValue(categoryName, out var minLevel) == true)
            return logLevel >= minLevel;

        return _logger.IsEnabled(logLevel);
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
        Func<TState, Exception, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        // Add custom enrichment
        var enrichedState = EnrichState(state);

        _logger.Log(logLevel, eventId, enrichedState, exception, formatter);
    }

    private TState EnrichState<TState>(TState state)
    {
        // Add custom properties to log state
        if (state is IEnumerable<KeyValuePair<string, object>> properties)
        {
            var enrichedProperties = properties.ToList();
            enrichedProperties.Add(new KeyValuePair<string, object>("Category", typeof(T).Name));
            enrichedProperties.Add(new KeyValuePair<string, object>("Timestamp", DateTimeOffset.UtcNow));

            return (TState)(object)enrichedProperties;
        }

        return state;
    }
}

public class LoggerOptions
{
    public bool Enabled { get; set; } = true;
    public string[] ExcludedCategories { get; set; }
    public Dictionary<string, LogLevel> CategoryLevels { get; set; }
}
```

## Serilog Configuration Examples

### Console Logging

```csharp
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "MyService")
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} " +
                       "{Properties:j}{NewLine}{Exception}")
    .CreateLogger();
```

### File Logging

```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.File(
        path: "logs/app-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        fileSizeLimitBytes: 10 * 1024 * 1024, // 10MB
        shared: true,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} " +
                       "{Properties:j}{NewLine}{Exception}")
    .CreateLogger();
```

### Seq Logging

```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.Seq("http://localhost:5341", apiKey: "your-api-key")
    .CreateLogger();
```

### Elasticsearch Logging

```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri("http://localhost:9200"))
    {
        IndexFormat = "myservice-logs-{0:yyyy.MM.dd}",
        AutoRegisterTemplate = true,
        AutoRegisterTemplateVersion = AutoRegisterTemplateVersion.ESv7
    })
    .CreateLogger();
```

## API Reference

### ILogger Extensions

```csharp
public static class LoggerExtensions
{
    // Structured logging helpers
    public static void LogTrace(this ILogger logger, string messageTemplate, params object[] propertyValues);
    public static void LogDebug(this ILogger logger, string messageTemplate, params object[] propertyValues);
    public static void LogInformation(this ILogger logger, string messageTemplate, params object[] propertyValues);
    public static void LogWarning(this ILogger logger, string messageTemplate, params object[] propertyValues);
    public static void LogError(this ILogger logger, string messageTemplate, params object[] propertyValues);
    public static void LogError(this ILogger logger, Exception exception, string messageTemplate, params object[] propertyValues);
    public static void LogCritical(this ILogger logger, string messageTemplate, params object[] propertyValues);
    public static void LogCritical(this ILogger logger, Exception exception, string messageTemplate, params object[] propertyValues);

    // Business domain helpers
    public static void LogBusiness(this ILogger logger, string messageTemplate, params object[] propertyValues);
    public static void LogSecurity(this ILogger logger, string messageTemplate, params object[] propertyValues);
    public static void LogPerformance(this ILogger logger, string messageTemplate, params object[] propertyValues);
    public static void LogIntegration(this ILogger logger, string messageTemplate, params object[] propertyValues);

    // Timing helpers
    public static IDisposable TimeOperation(this ILogger logger, string operationName);
    public static IDisposable TimeOperation(this ILogger logger, string operationName, LogLevel logLevel);
}
```

### AddLogging() Options

```csharp
public static IConveyBuilder AddLogging(this IConveyBuilder builder, string sectionName = "logger")
public static IConveyBuilder AddConsoleLogging(this IConveyBuilder builder)
public static IConveyBuilder AddFileLogging(this IConveyBuilder builder, string path = "logs/app.txt")
public static IConveyBuilder AddSeqLogging(this IConveyBuilder builder, string url, string apiKey = null)
```

## Best Practices

1. **Use structured logging** - Always use message templates with named parameters
2. **Log at appropriate levels** - Use Debug for developer info, Information for business events, Warning for recoverable errors
3. **Include context** - Use correlation IDs and scoped properties for traceability
4. **Don't log sensitive data** - Avoid logging passwords, tokens, or personal information
5. **Use performance logging** - Log execution times for critical operations
6. **Implement log enrichers** - Add contextual information automatically
7. **Configure log retention** - Set appropriate retention policies for different environments
8. **Use semantic logging** - Log business events, not just technical details

## Troubleshooting

### Common Issues

1. **Logs not appearing**
   - Check log level configuration
   - Verify output configuration (console, file, etc.)
   - Ensure logger is properly configured before application startup

2. **Performance issues**
   - Use async logging where possible
   - Configure appropriate buffer sizes
   - Consider using sampling for high-volume scenarios

3. **Missing context**
   - Ensure enrichers are properly registered
   - Check scope usage in async operations
   - Verify HTTP context is available for enrichers

4. **File logging issues**
   - Check file permissions
   - Verify disk space availability
   - Ensure file paths are accessible

