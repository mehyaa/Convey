---
layout: default
title: Convey.Tracing.Jaeger
parent: Observability
---
# Convey.Tracing.Jaeger

Distributed tracing integration with Jaeger providing comprehensive request tracing, performance monitoring, and debugging capabilities for microservices architectures.

## Installation

```bash
dotnet add package Convey.Tracing.Jaeger
```

## Overview

Convey.Tracing.Jaeger provides:
- **Distributed tracing** - End-to-end request tracing across services
- **Jaeger integration** - Native integration with Jaeger tracing system
- **OpenTelemetry support** - Built on OpenTelemetry standards
- **Automatic instrumentation** - HTTP, database, and framework instrumentation
- **Custom spans** - Create custom spans for business operations
- **Performance insights** - Detailed timing and dependency analysis
- **Error tracking** - Trace error propagation across services

## Configuration

### Basic Setup

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddJaeger(); // Enables Jaeger tracing

var app = builder.Build();

app.Run();
```

### Advanced Configuration

Configure in `appsettings.json`:

```json
{
  "jaeger": {
    "enabled": true,
    "serviceName": "my-service",
    "udpHost": "localhost",
    "udpPort": 6831,
    "maxPacketSize": 0,
    "sampler": "const",
    "maxTracesPerSecond": 5,
    "samplingRate": 1.0,
    "excludePaths": ["/health", "/metrics"],
    "tags": {
      "version": "1.0.0",
      "environment": "production"
    }
  }
}
```

### Full Configuration

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddJaeger(jaegerOptions =>
    {
        jaegerOptions.ServiceName = "order-service";
        jaegerOptions.UdpHost = "jaeger-agent";
        jaegerOptions.UdpPort = 6831;
        jaegerOptions.SamplingRate = 1.0; // Sample all traces in development
        jaegerOptions.ExcludePaths = new[] { "/health", "/metrics", "/swagger" };
    });

var app = builder.Build();

// Enable tracing middleware
app.UseTracing(); // Automatically traces HTTP requests

app.Run();
```

## Key Features

### 1. Automatic HTTP Tracing

HTTP requests are automatically traced:

```csharp
// HTTP requests are automatically instrumented when UseTracing() is configured
app.UseTracing(); // Tracks all HTTP requests with spans

// Automatic spans created for:
// - Incoming HTTP requests
// - Outgoing HTTP requests
// - Database operations
// - Message broker operations
```

### 2. Custom Spans

Create custom spans for business operations:

```csharp
public class OrderService
{
    private readonly ITracer _tracer;
    private readonly IOrderRepository _repository;

    public OrderService(ITracer tracer, IOrderRepository repository)
    {
        _tracer = tracer;
        _repository = repository;
    }

    public async Task<OrderResult> ProcessOrderAsync(ProcessOrderCommand command)
    {
        using var span = _tracer.StartActiveSpan("order.process")
            .SetTag("order.id", command.OrderId.ToString())
            .SetTag("order.type", command.Type)
            .SetTag("customer.id", command.CustomerId.ToString());

        try
        {
            // Validate order
            using var validationSpan = _tracer.StartActiveSpan("order.validate");
            await ValidateOrderAsync(command);
            validationSpan.SetTag("validation.result", "success");

            // Process payment
            using var paymentSpan = _tracer.StartActiveSpan("order.payment")
                .SetTag("payment.amount", command.Amount)
                .SetTag("payment.method", command.PaymentMethod);

            var paymentResult = await ProcessPaymentAsync(command);
            paymentSpan.SetTag("payment.transaction_id", paymentResult.TransactionId);

            // Update inventory
            using var inventorySpan = _tracer.StartActiveSpan("order.inventory");
            await UpdateInventoryAsync(command);

            // Save order
            using var saveSpan = _tracer.StartActiveSpan("order.save");
            var order = await _repository.SaveAsync(command);

            span.SetTag("order.result", "success")
                .SetTag("order.total_amount", command.Amount);

            return new OrderResult { OrderId = order.Id, Success = true };
        }
        catch (ValidationException ex)
        {
            span.SetTag("error", true)
                .SetTag("error.type", "validation")
                .SetTag("error.message", ex.Message);
            throw;
        }
        catch (PaymentException ex)
        {
            span.SetTag("error", true)
                .SetTag("error.type", "payment")
                .SetTag("error.message", ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            span.SetTag("error", true)
                .SetTag("error.type", "unknown")
                .SetTag("error.message", ex.Message);
            throw;
        }
    }
}
```

### 3. Tracing Decorators

Automatically trace handlers with decorators:

```csharp
// Command handler tracing decorator
public class TracingCommandHandlerDecorator<TCommand> : ICommandHandler<TCommand>
    where TCommand : class, ICommand
{
    private readonly ICommandHandler<TCommand> _handler;
    private readonly ITracer _tracer;

    public TracingCommandHandlerDecorator(ICommandHandler<TCommand> handler, ITracer tracer)
    {
        _handler = handler;
        _tracer = tracer;
    }

    public async Task HandleAsync(TCommand command, CancellationToken cancellationToken = default)
    {
        var commandName = typeof(TCommand).Name;

        using var span = _tracer.StartActiveSpan($"command.{commandName}")
            .SetTag("command.type", commandName)
            .SetTag("component", "command-handler");

        // Add command-specific tags
        if (command is IIdentifiable identifiable)
        {
            span.SetTag("command.id", identifiable.Id.ToString());
        }

        try
        {
            await _handler.HandleAsync(command, cancellationToken);
            span.SetTag("command.result", "success");
        }
        catch (ValidationException ex)
        {
            span.SetTag("error", true)
                .SetTag("error.type", "validation")
                .SetTag("error.message", ex.Message)
                .SetTag("command.result", "validation_error");
            throw;
        }
        catch (Exception ex)
        {
            span.SetTag("error", true)
                .SetTag("error.type", ex.GetType().Name)
                .SetTag("error.message", ex.Message)
                .SetTag("command.result", "error");
            throw;
        }
    }
}

// Query handler tracing decorator
public class TracingQueryHandlerDecorator<TQuery, TResult> : IQueryHandler<TQuery, TResult>
    where TQuery : class, IQuery<TResult>
{
    private readonly IQueryHandler<TQuery, TResult> _handler;
    private readonly ITracer _tracer;

    public TracingQueryHandlerDecorator(IQueryHandler<TQuery, TResult> handler, ITracer tracer)
    {
        _handler = handler;
        _tracer = tracer;
    }

    public async Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken = default)
    {
        var queryName = typeof(TQuery).Name;

        using var span = _tracer.StartActiveSpan($"query.{queryName}")
            .SetTag("query.type", queryName)
            .SetTag("component", "query-handler");

        // Add query-specific tags
        if (query is IPagedQuery pagedQuery)
        {
            span.SetTag("query.page", pagedQuery.Page)
                .SetTag("query.page_size", pagedQuery.PageSize);
        }

        try
        {
            var result = await _handler.HandleAsync(query, cancellationToken);

            span.SetTag("query.result", "success");

            // Add result information
            if (result is IPagedResult pagedResult)
            {
                span.SetTag("result.total_items", pagedResult.TotalItems)
                    .SetTag("result.total_pages", pagedResult.TotalPages);
            }

            return result;
        }
        catch (Exception ex)
        {
            span.SetTag("error", true)
                .SetTag("error.type", ex.GetType().Name)
                .SetTag("error.message", ex.Message)
                .SetTag("query.result", "error");
            throw;
        }
    }
}

// Registration
builder.Services.TryDecorate(typeof(ICommandHandler<>), typeof(TracingCommandHandlerDecorator<>));
builder.Services.TryDecorate(typeof(IQueryHandler<,>), typeof(TracingQueryHandlerDecorator<,>));
```

### 4. Database Operation Tracing

Trace database operations:

```csharp
public class TracingUserRepository : IUserRepository
{
    private readonly IUserRepository _repository;
    private readonly ITracer _tracer;

    public TracingUserRepository(IUserRepository repository, ITracer tracer)
    {
        _repository = repository;
        _tracer = tracer;
    }

    public async Task<User> GetByIdAsync(Guid id)
    {
        using var span = _tracer.StartActiveSpan("db.query.users.get_by_id")
            .SetTag("db.type", "mongodb")
            .SetTag("db.collection", "users")
            .SetTag("db.operation", "find_one")
            .SetTag("user.id", id.ToString());

        try
        {
            var user = await _repository.GetByIdAsync(id);

            span.SetTag("db.result", user != null ? "found" : "not_found");

            return user;
        }
        catch (Exception ex)
        {
            span.SetTag("error", true)
                .SetTag("error.type", ex.GetType().Name)
                .SetTag("error.message", ex.Message);
            throw;
        }
    }

    public async Task<PagedResult<User>> BrowseAsync(BrowseUsersQuery query)
    {
        using var span = _tracer.StartActiveSpan("db.query.users.browse")
            .SetTag("db.type", "mongodb")
            .SetTag("db.collection", "users")
            .SetTag("db.operation", "find")
            .SetTag("query.page", query.Page)
            .SetTag("query.page_size", query.PageSize);

        try
        {
            var result = await _repository.BrowseAsync(query);

            span.SetTag("db.result_count", result.Items.Count())
                .SetTag("db.total_count", result.TotalItems);

            return result;
        }
        catch (Exception ex)
        {
            span.SetTag("error", true)
                .SetTag("error.type", ex.GetType().Name)
                .SetTag("error.message", ex.Message);
            throw;
        }
    }

    public async Task AddAsync(User user)
    {
        using var span = _tracer.StartActiveSpan("db.command.users.insert")
            .SetTag("db.type", "mongodb")
            .SetTag("db.collection", "users")
            .SetTag("db.operation", "insert_one")
            .SetTag("user.id", user.Id.ToString());

        try
        {
            await _repository.AddAsync(user);
            span.SetTag("db.result", "success");
        }
        catch (Exception ex)
        {
            span.SetTag("error", true)
                .SetTag("error.type", ex.GetType().Name)
                .SetTag("error.message", ex.Message);
            throw;
        }
    }
}
```

### 5. External Service Tracing

Trace calls to external services:

```csharp
public class TracingPaymentService : IPaymentService
{
    private readonly IPaymentService _paymentService;
    private readonly ITracer _tracer;
    private readonly HttpClient _httpClient;

    public TracingPaymentService(IPaymentService paymentService, ITracer tracer, HttpClient httpClient)
    {
        _paymentService = paymentService;
        _tracer = tracer;
        _httpClient = httpClient;
    }

    public async Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request)
    {
        using var span = _tracer.StartActiveSpan("external.payment.process")
            .SetTag("service.name", "payment-service")
            .SetTag("service.type", "external")
            .SetTag("payment.amount", request.Amount)
            .SetTag("payment.currency", request.Currency)
            .SetTag("payment.method", request.PaymentMethod);

        try
        {
            // Inject trace context into HTTP headers
            var headers = new Dictionary<string, string>();
            _tracer.Inject(span.Context, Format.HttpHeaders, new TextMapInjectAdapter(headers));

            foreach (var header in headers)
            {
                _httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
            }

            var result = await _paymentService.ProcessPaymentAsync(request);

            span.SetTag("payment.transaction_id", result.TransactionId)
                .SetTag("payment.status", result.Status)
                .SetTag("external.result", "success");

            return result;
        }
        catch (PaymentException ex)
        {
            span.SetTag("error", true)
                .SetTag("error.type", "payment_error")
                .SetTag("error.code", ex.ErrorCode)
                .SetTag("error.message", ex.Message);
            throw;
        }
        catch (HttpRequestException ex)
        {
            span.SetTag("error", true)
                .SetTag("error.type", "http_error")
                .SetTag("error.message", ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            span.SetTag("error", true)
                .SetTag("error.type", ex.GetType().Name)
                .SetTag("error.message", ex.Message);
            throw;
        }
    }
}

// HTTP client tracing
public class TracingHttpMessageHandler : DelegatingHandler
{
    private readonly ITracer _tracer;

    public TracingHttpMessageHandler(ITracer tracer)
    {
        _tracer = tracer;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var span = _tracer.StartActiveSpan("http.client.request")
            .SetTag("http.method", request.Method.ToString())
            .SetTag("http.url", request.RequestUri?.ToString())
            .SetTag("component", "http-client");

        // Inject trace context
        var headers = new Dictionary<string, string>();
        _tracer.Inject(span.Context, Format.HttpHeaders, new TextMapInjectAdapter(headers));

        foreach (var header in headers)
        {
            request.Headers.Add(header.Key, header.Value);
        }

        try
        {
            var response = await base.SendAsync(request, cancellationToken);

            span.SetTag("http.status_code", (int)response.StatusCode)
                .SetTag("http.response_size", response.Content.Headers.ContentLength ?? 0);

            if (!response.IsSuccessStatusCode)
            {
                span.SetTag("error", true);
            }

            return response;
        }
        catch (Exception ex)
        {
            span.SetTag("error", true)
                .SetTag("error.type", ex.GetType().Name)
                .SetTag("error.message", ex.Message);
            throw;
        }
    }
}

// Registration
builder.Services.AddHttpClient<IPaymentService, PaymentService>()
    .AddHttpMessageHandler<TracingHttpMessageHandler>();
```

## Advanced Features

### 1. Correlation ID Propagation

Propagate correlation IDs across services:

```csharp
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ITracer _tracer;

    public CorrelationIdMiddleware(RequestDelegate next, ITracer tracer)
    {
        _next = next;
        _tracer = tracer;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
                           ?? Guid.NewGuid().ToString();

        context.Items["CorrelationId"] = correlationId;
        context.Response.Headers["X-Correlation-ID"] = correlationId;

        // Add correlation ID to current span
        var activeSpan = _tracer.ActiveSpan;
        if (activeSpan != null)
        {
            activeSpan.SetTag("correlation.id", correlationId);
        }

        await _next(context);
    }
}

// Service that uses correlation ID
public class NotificationService
{
    private readonly ITracer _tracer;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public async Task SendNotificationAsync(SendNotificationCommand command)
    {
        var correlationId = _httpContextAccessor.HttpContext?.Items["CorrelationId"]?.ToString();

        using var span = _tracer.StartActiveSpan("notification.send")
            .SetTag("correlation.id", correlationId)
            .SetTag("notification.type", command.Type)
            .SetTag("recipient.id", command.RecipientId);

        // Implementation...
    }
}
```

### 2. Baggage Propagation

Use baggage to propagate additional context:

```csharp
public class BaggageMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ITracer _tracer;

    public BaggageMiddleware(RequestDelegate next, ITracer tracer)
    {
        _next = next;
        _tracer = tracer;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var activeSpan = _tracer.ActiveSpan;
        if (activeSpan != null)
        {
            // Set baggage from headers or context
            var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                activeSpan.SetBaggageItem("user.id", userId);
            }

            var tenantId = context.Request.Headers["X-Tenant-ID"].FirstOrDefault();
            if (!string.IsNullOrEmpty(tenantId))
            {
                activeSpan.SetBaggageItem("tenant.id", tenantId);
            }
        }

        await _next(context);
    }
}

// Service that uses baggage
public class AuditService
{
    private readonly ITracer _tracer;

    public async Task LogActionAsync(string action, object data)
    {
        var activeSpan = _tracer.ActiveSpan;
        var userId = activeSpan?.GetBaggageItem("user.id");
        var tenantId = activeSpan?.GetBaggageItem("tenant.id");

        using var span = _tracer.StartActiveSpan("audit.log")
            .SetTag("audit.action", action)
            .SetTag("audit.user_id", userId)
            .SetTag("audit.tenant_id", tenantId);

        // Log audit event with context
        await LogAuditEvent(action, data, userId, tenantId);
    }
}
```

### 3. Sampling Strategies

Implement custom sampling strategies:

```csharp
public class CustomSampler : ISampler
{
    private readonly double _defaultSamplingRate;
    private readonly Dictionary<string, double> _operationSamplingRates;

    public CustomSampler(double defaultSamplingRate, Dictionary<string, double> operationSamplingRates = null)
    {
        _defaultSamplingRate = defaultSamplingRate;
        _operationSamplingRates = operationSamplingRates ?? new Dictionary<string, double>();
    }

    public SamplingResult Sample(string operationName, TraceId traceId)
    {
        var samplingRate = _operationSamplingRates.GetValueOrDefault(operationName, _defaultSamplingRate);

        // Special sampling rules
        if (operationName.Contains("health") || operationName.Contains("metrics"))
        {
            return SamplingResult.Create(false); // Don't sample health/metrics endpoints
        }

        if (operationName.Contains("error") || operationName.Contains("exception"))
        {
            return SamplingResult.Create(true); // Always sample errors
        }

        // Use trace ID for deterministic sampling
        var traceIdBytes = traceId.ToByteArray();
        var hash = BitConverter.ToUInt64(traceIdBytes, 0);
        var shouldSample = (hash % 10000) < (samplingRate * 10000);

        return SamplingResult.Create(shouldSample);
    }
}

// Registration
builder.Services.AddJaeger(options =>
{
    options.Sampler = new CustomSampler(0.1, new Dictionary<string, double>
    {
        ["order.process"] = 1.0,    // Sample all order processing
        ["payment.process"] = 1.0,  // Sample all payment processing
        ["user.login"] = 0.5,       // Sample 50% of logins
        ["search.query"] = 0.01     // Sample 1% of searches
    });
});
```

## Integration Patterns

### 1. Message Broker Integration

Trace message processing:

```csharp
public class TracingEventHandler<TEvent> : IEventHandler<TEvent>
    where TEvent : class, IEvent
{
    private readonly IEventHandler<TEvent> _handler;
    private readonly ITracer _tracer;

    public TracingEventHandler(IEventHandler<TEvent> handler, ITracer tracer)
    {
        _handler = handler;
        _tracer = tracer;
    }

    public async Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default)
    {
        var eventName = typeof(TEvent).Name;

        // Extract trace context from event metadata
        ISpanContext parentContext = null;
        if (@event is ITraceable traceable && traceable.TraceContext != null)
        {
            parentContext = ExtractTraceContext(traceable.TraceContext);
        }

        var spanBuilder = _tracer.BuildSpan($"event.{eventName}")
            .WithTag("event.type", eventName)
            .WithTag("component", "event-handler");

        if (parentContext != null)
        {
            spanBuilder = spanBuilder.AsChildOf(parentContext);
        }

        using var span = spanBuilder.Start();

        try
        {
            await _handler.HandleAsync(@event, cancellationToken);
            span.SetTag("event.result", "success");
        }
        catch (Exception ex)
        {
            span.SetTag("error", true)
                .SetTag("error.type", ex.GetType().Name)
                .SetTag("error.message", ex.Message);
            throw;
        }
    }

    private ISpanContext ExtractTraceContext(Dictionary<string, string> traceContext)
    {
        try
        {
            return _tracer.Extract(Format.TextMap, new TextMapExtractAdapter(traceContext));
        }
        catch
        {
            return null;
        }
    }
}

// Event with trace context
public interface ITraceable
{
    Dictionary<string, string> TraceContext { get; set; }
}

public class OrderCreatedEvent : IEvent, ITraceable
{
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public decimal Amount { get; set; }
    public Dictionary<string, string> TraceContext { get; set; }
}

// Publish event with trace context
public class EventPublisher
{
    private readonly ITracer _tracer;
    private readonly IEventDispatcher _dispatcher;

    public async Task PublishAsync<TEvent>(TEvent @event) where TEvent : class, IEvent
    {
        if (@event is ITraceable traceable)
        {
            var traceContext = new Dictionary<string, string>();
            _tracer.Inject(_tracer.ActiveSpan.Context, Format.TextMap, new TextMapInjectAdapter(traceContext));
            traceable.TraceContext = traceContext;
        }

        await _dispatcher.PublishAsync(@event);
    }
}
```

## Configuration Options

### Jaeger Settings

```csharp
public class JaegerOptions
{
    public bool Enabled { get; set; } = true;
    public string ServiceName { get; set; }
    public string UdpHost { get; set; } = "localhost";
    public int UdpPort { get; set; } = 6831;
    public int MaxPacketSize { get; set; } = 0;
    public string Sampler { get; set; } = "const";
    public double SamplingRate { get; set; } = 1.0;
    public int MaxTracesPerSecond { get; set; } = 5;
    public string[] ExcludePaths { get; set; } = Array.Empty<string>();
    public Dictionary<string, object> Tags { get; set; } = new();
}
```

## API Reference

### ITracer Interface

```csharp
public interface ITracer
{
    ISpan ActiveSpan { get; }
    ISpanBuilder BuildSpan(string operationName);
    ISpan StartActiveSpan(string operationName);
    void Inject<TCarrier>(ISpanContext spanContext, IFormat<TCarrier> format, TCarrier carrier);
    ISpanContext Extract<TCarrier>(IFormat<TCarrier> format, TCarrier carrier);
}
```

### ISpan Interface

```csharp
public interface ISpan : IDisposable
{
    ISpanContext Context { get; }
    ISpan SetTag(string key, string value);
    ISpan SetTag(string key, bool value);
    ISpan SetTag(string key, int value);
    ISpan SetTag(string key, double value);
    ISpan SetBaggageItem(string key, string value);
    string GetBaggageItem(string key);
    ISpan Log(string message);
    ISpan Log(Dictionary<string, object> fields);
    void Finish();
}
```

### Extension Methods

```csharp
public static class ConveyExtensions
{
    public static IConveyBuilder AddJaeger(this IConveyBuilder builder, string sectionName = "jaeger");
    public static IConveyBuilder AddJaeger(this IConveyBuilder builder, Action<JaegerOptions> configure);
    public static IApplicationBuilder UseTracing(this IApplicationBuilder app);
}
```

## Best Practices

1. **Use meaningful span names** - Use clear, hierarchical naming conventions
2. **Add relevant tags** - Include business context and technical details
3. **Handle errors properly** - Mark spans with error tags and log exceptions
4. **Use appropriate sampling** - Balance observability with performance
5. **Propagate context** - Ensure trace context flows across service boundaries
6. **Monitor trace performance** - Keep tracing overhead minimal
7. **Use child spans** - Break down complex operations into smaller spans
8. **Include business metrics** - Add business-relevant tags and logs

## Troubleshooting

### Common Issues

1. **Traces not appearing in Jaeger**
   - Check Jaeger agent connectivity
   - Verify service name configuration
   - Ensure sampling is not too restrictive

2. **Missing trace context**
   - Verify context propagation in HTTP headers
   - Check async/await patterns for context flow
   - Ensure proper decorator registration

3. **Performance impact**
   - Adjust sampling rates for high-volume endpoints
   - Use asynchronous reporting
   - Monitor memory usage of trace buffers

4. **Incomplete traces**
   - Check for exceptions that terminate spans early
   - Verify proper span disposal with using statements
   - Ensure child spans are properly linked to parents

