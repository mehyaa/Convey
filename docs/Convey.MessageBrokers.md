---
layout: default
title: Convey.MessageBrokers
parent: Message Brokers
---
# Convey.MessageBrokers

Message broker abstractions providing unified messaging patterns, event publishing/subscribing, and seamless integration with various message broker implementations for building event-driven architectures.

## Installation

```bash
dotnet add package Convey.MessageBrokers
```

## Overview

Convey.MessageBrokers provides:
- **Unified messaging interface** - Common abstractions for different message brokers
- **Event publishing** - Reliable event publishing with various delivery guarantees
- **Message handling** - Automatic message routing and handler invocation
- **Serialization support** - JSON and binary message serialization
- **Error handling** - Dead letter queues and retry mechanisms
- **Message correlation** - Request-response patterns and correlation tracking
- **Performance optimization** - Connection pooling and message batching

## Configuration

### Basic Setup

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddRabbitMq(); // Add specific message broker implementation

var app = builder.Build();

app.Run();
```

### Advanced Configuration

Configure in `appsettings.json`:

```json
{
  "messageBrokers": {
    "messageBroker": {
      "type": "rabbitmq"
    }
  }
}
```

### Full Configuration with RabbitMQ

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddRabbitMq() // Specific message broker implementation
    .AddCommandHandlers() // Register command handlers (from Convey.CQRS.Commands)
    .AddEventHandlers() // Register event handlers (from Convey.CQRS.Events)
    .AddInMemoryEventDispatcher(); // Local event dispatching

var app = builder.Build();

app.UseRabbitMq(); // Subscribe to messages

app.Run();
```

## Key Features

### 1. Event Publishing

Publish events to message brokers:

```csharp
// Event definitions
public interface IEvent
{
    DateTime OccurredAt { get; }
    string EventId { get; }
}

public abstract class EventBase : IEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventId { get; } = Guid.NewGuid().ToString();
}

public class UserCreatedEvent : EventBase
{
    public Guid UserId { get; }
    public string Email { get; }
    public string Name { get; }
    public DateTime CreatedAt { get; }

    public UserCreatedEvent(Guid userId, string email, string name)
    {
        UserId = userId;
        Email = email;
        Name = name;
        CreatedAt = DateTime.UtcNow;
    }
}

public class OrderPlacedEvent : EventBase
{
    public Guid OrderId { get; }
    public Guid CustomerId { get; }
    public decimal TotalAmount { get; }
    public IEnumerable<OrderItem> Items { get; }

    public OrderPlacedEvent(Guid orderId, Guid customerId, decimal totalAmount, IEnumerable<OrderItem> items)
    {
        OrderId = orderId;
        CustomerId = customerId;
        TotalAmount = totalAmount;
        Items = items;
    }
}

// Event publisher service
public class OrderService
{
    private readonly IEventDispatcher _eventDispatcher;
    private readonly IOrderRepository _orderRepository;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IEventDispatcher eventDispatcher,
        IOrderRepository orderRepository,
        ILogger<OrderService> logger)
    {
        _eventDispatcher = eventDispatcher;
        _orderRepository = orderRepository;
        _logger = logger;
    }

    public async Task CreateOrderAsync(CreateOrderCommand command)
    {
        _logger.LogInformation("Creating order for customer {CustomerId}", command.CustomerId);

        // Create order
        var order = new Order(command.CustomerId, command.Items);
        await _orderRepository.AddAsync(order);

        // Publish event
        var orderPlacedEvent = new OrderPlacedEvent(
            order.Id,
            order.CustomerId,
            order.TotalAmount,
            order.Items);

        await _eventDispatcher.PublishAsync(orderPlacedEvent);

        _logger.LogInformation("Order {OrderId} created and event published", order.Id);
    }
}

// Event dispatcher interface
public interface IEventDispatcher
{
    Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default) where T : class, IEvent;
    Task PublishAsync<T>(T @event, string routingKey, CancellationToken cancellationToken = default) where T : class, IEvent;
}
```

### 2. Message Handling

Handle incoming messages with automatic routing:

```csharp
// Event handlers
public interface IEventHandler<in TEvent> where TEvent : class, IEvent
{
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
}

// User creation handler
public class UserCreatedEventHandler : IEventHandler<UserCreatedEvent>
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<UserCreatedEventHandler> _logger;

    public UserCreatedEventHandler(
        INotificationService notificationService,
        ILogger<UserCreatedEventHandler> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task HandleAsync(UserCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Handling user created event for user {UserId}", @event.UserId);

        try
        {
            // Send welcome email
            await _notificationService.SendWelcomeEmailAsync(@event.UserId, @event.Email, @event.Name);

            // Update analytics
            await _notificationService.TrackUserRegistrationAsync(@event.UserId);

            _logger.LogInformation("Successfully processed user created event for user {UserId}", @event.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process user created event for user {UserId}", @event.UserId);
            throw; // Will be handled by message broker retry/dead letter logic
        }
    }
}

// Order placement handler
public class OrderPlacedEventHandler : IEventHandler<OrderPlacedEvent>
{
    private readonly IInventoryService _inventoryService;
    private readonly IPaymentService _paymentService;
    private readonly ILogger<OrderPlacedEventHandler> _logger;

    public OrderPlacedEventHandler(
        IInventoryService inventoryService,
        IPaymentService paymentService,
        ILogger<OrderPlacedEventHandler> logger)
    {
        _inventoryService = inventoryService;
        _paymentService = paymentService;
        _logger = logger;
    }

    public async Task HandleAsync(OrderPlacedEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing order {OrderId} for customer {CustomerId}",
            @event.OrderId, @event.CustomerId);

        try
        {
            // Reserve inventory
            await _inventoryService.ReserveItemsAsync(@event.OrderId, @event.Items);

            // Process payment
            var paymentResult = await _paymentService.ProcessPaymentAsync(
                @event.CustomerId, @event.TotalAmount, @event.OrderId);

            if (paymentResult.Success)
            {
                _logger.LogInformation("Order {OrderId} processed successfully", @event.OrderId);
            }
            else
            {
                _logger.LogWarning("Payment failed for order {OrderId}: {Reason}",
                    @event.OrderId, paymentResult.FailureReason);

                // Release inventory
                await _inventoryService.ReleaseItemsAsync(@event.OrderId);

                throw new PaymentFailedException($"Payment failed: {paymentResult.FailureReason}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process order {OrderId}", @event.OrderId);
            throw;
        }
    }
}

// Multiple handlers for same event
public class OrderAnalyticsEventHandler : IEventHandler<OrderPlacedEvent>
{
    private readonly IAnalyticsService _analyticsService;

    public OrderAnalyticsEventHandler(IAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    public async Task HandleAsync(OrderPlacedEvent @event, CancellationToken cancellationToken = default)
    {
        // Track order analytics
        await _analyticsService.TrackOrderPlacedAsync(@event.OrderId, @event.TotalAmount);
    }
}
```

### 3. Message Serialization

Configure message serialization:

```csharp
// Message serializer interface
public interface IMessageSerializer
{
    string Serialize<T>(T message);
    T Deserialize<T>(string serializedMessage);
    object Deserialize(string serializedMessage, Type type);
}

// JSON message serializer
public class JsonMessageSerializer : IMessageSerializer
{
    private readonly JsonSerializerOptions _options;

    public JsonMessageSerializer(JsonSerializerOptions options = null)
    {
        _options = options ?? new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    public string Serialize<T>(T message)
    {
        return JsonSerializer.Serialize(message, _options);
    }

    public T Deserialize<T>(string serializedMessage)
    {
        return JsonSerializer.Deserialize<T>(serializedMessage, _options);
    }

    public object Deserialize(string serializedMessage, Type type)
    {
        return JsonSerializer.Deserialize(serializedMessage, type, _options);
    }
}

// Binary message serializer (using MessagePack)
public class BinaryMessageSerializer : IMessageSerializer
{
    public string Serialize<T>(T message)
    {
        var bytes = MessagePackSerializer.Serialize(message);
        return Convert.ToBase64String(bytes);
    }

    public T Deserialize<T>(string serializedMessage)
    {
        var bytes = Convert.FromBase64String(serializedMessage);
        return MessagePackSerializer.Deserialize<T>(bytes);
    }

    public object Deserialize(string serializedMessage, Type type)
    {
        var bytes = Convert.FromBase64String(serializedMessage);
        return MessagePackSerializer.Deserialize(type, bytes);
    }
}
```

### 4. Message Correlation and Request-Response

Implement request-response messaging patterns:

```csharp
// Request-response interfaces
public interface IRequest
{
    string RequestId { get; }
    string CorrelationId { get; }
}

public interface IResponse
{
    string RequestId { get; }
    string CorrelationId { get; }
    bool Success { get; }
    string ErrorMessage { get; }
}

// Request-response message types
public class GetUserDetailsRequest : IRequest
{
    public string RequestId { get; } = Guid.NewGuid().ToString();
    public string CorrelationId { get; set; }
    public Guid UserId { get; set; }
}

public class GetUserDetailsResponse : IResponse
{
    public string RequestId { get; set; }
    public string CorrelationId { get; set; }
    public bool Success { get; set; }
    public string ErrorMessage { get; set; }
    public UserDetails User { get; set; }
}

// Request-response service
public interface IRequestResponseService
{
    Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : class, IRequest
        where TResponse : class, IResponse;
}

public class RequestResponseService : IRequestResponseService
{
    private readonly IEventDispatcher _eventDispatcher;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<object>> _pendingRequests;
    private readonly ILogger<RequestResponseService> _logger;

    public RequestResponseService(IEventDispatcher eventDispatcher, ILogger<RequestResponseService> logger)
    {
        _eventDispatcher = eventDispatcher;
        _pendingRequests = new ConcurrentDictionary<string, TaskCompletionSource<object>>();
        _logger = logger;
    }

    public async Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : class, IRequest
        where TResponse : class, IResponse
    {
        var tcs = new TaskCompletionSource<object>();
        _pendingRequests[request.RequestId] = tcs;

        try
        {
            // Publish request
            await _eventDispatcher.PublishAsync(request, cancellationToken);

            // Wait for response (with timeout)
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            var response = await tcs.Task.WaitAsync(combinedCts.Token);
            return (TResponse)response;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Request {RequestId} timed out", request.RequestId);
            throw new TimeoutException($"Request {request.RequestId} timed out");
        }
        finally
        {
            _pendingRequests.TryRemove(request.RequestId, out _);
        }
    }

    public void HandleResponse<TResponse>(TResponse response) where TResponse : class, IResponse
    {
        if (_pendingRequests.TryRemove(response.RequestId, out var tcs))
        {
            tcs.SetResult(response);
        }
    }
}

// Request handler
public class GetUserDetailsRequestHandler : IEventHandler<GetUserDetailsRequest>
{
    private readonly IUserService _userService;
    private readonly IEventDispatcher _eventDispatcher;

    public GetUserDetailsRequestHandler(IUserService userService, IEventDispatcher eventDispatcher)
    {
        _userService = userService;
        _eventDispatcher = eventDispatcher;
    }

    public async Task HandleAsync(GetUserDetailsRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await _userService.GetUserDetailsAsync(request.UserId);

            var response = new GetUserDetailsResponse
            {
                RequestId = request.RequestId,
                CorrelationId = request.CorrelationId,
                Success = true,
                User = user
            };

            await _eventDispatcher.PublishAsync(response, cancellationToken);
        }
        catch (Exception ex)
        {
            var response = new GetUserDetailsResponse
            {
                RequestId = request.RequestId,
                CorrelationId = request.CorrelationId,
                Success = false,
                ErrorMessage = ex.Message
            };

            await _eventDispatcher.PublishAsync(response, cancellationToken);
        }
    }
}
```

### 5. Error Handling and Dead Letter Queues

Handle message processing failures:

```csharp
// Error handling wrapper
public class ErrorHandlingEventHandler<TEvent> : IEventHandler<TEvent>
    where TEvent : class, IEvent
{
    private readonly IEventHandler<TEvent> _handler;
    private readonly IDeadLetterService _deadLetterService;
    private readonly ILogger<ErrorHandlingEventHandler<TEvent>> _logger;

    public ErrorHandlingEventHandler(
        IEventHandler<TEvent> handler,
        IDeadLetterService deadLetterService,
        ILogger<ErrorHandlingEventHandler<TEvent>> logger)
    {
        _handler = handler;
        _deadLetterService = deadLetterService;
        _logger = logger;
    }

    public async Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default)
    {
        var eventType = typeof(TEvent).Name;
        var eventId = @event.EventId;

        try
        {
            await _handler.HandleAsync(@event, cancellationToken);
            _logger.LogDebug("Successfully processed {EventType} event {EventId}", eventType, eventId);
        }
        catch (TransientException ex)
        {
            _logger.LogWarning(ex, "Transient error processing {EventType} event {EventId}, will retry",
                eventType, eventId);
            throw; // Let message broker handle retry
        }
        catch (PermanentException ex)
        {
            _logger.LogError(ex, "Permanent error processing {EventType} event {EventId}, sending to dead letter",
                eventType, eventId);

            await _deadLetterService.SendToDeadLetterAsync(@event, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unknown error processing {EventType} event {EventId}", eventType, eventId);
            throw; // Let message broker decide retry/dead letter behavior
        }
    }
}

// Dead letter service
public interface IDeadLetterService
{
    Task SendToDeadLetterAsync<T>(T message, string reason) where T : class;
    Task<IEnumerable<DeadLetterMessage>> GetDeadLetterMessagesAsync();
    Task ReprocessDeadLetterMessageAsync(string messageId);
}

public class DeadLetterService : IDeadLetterService
{
    private readonly IEventDispatcher _eventDispatcher;
    private readonly IDeadLetterRepository _repository;
    private readonly ILogger<DeadLetterService> _logger;

    public DeadLetterService(
        IEventDispatcher eventDispatcher,
        IDeadLetterRepository repository,
        ILogger<DeadLetterService> logger)
    {
        _eventDispatcher = eventDispatcher;
        _repository = repository;
        _logger = logger;
    }

    public async Task SendToDeadLetterAsync<T>(T message, string reason) where T : class
    {
        var deadLetterMessage = new DeadLetterMessage
        {
            Id = Guid.NewGuid().ToString(),
            MessageType = typeof(T).Name,
            MessageContent = JsonSerializer.Serialize(message),
            Reason = reason,
            CreatedAt = DateTime.UtcNow
        };

        await _repository.AddAsync(deadLetterMessage);

        _logger.LogInformation("Message sent to dead letter queue: {MessageType} - {Reason}",
            deadLetterMessage.MessageType, reason);
    }

    public async Task<IEnumerable<DeadLetterMessage>> GetDeadLetterMessagesAsync()
    {
        return await _repository.GetAllAsync();
    }

    public async Task ReprocessDeadLetterMessageAsync(string messageId)
    {
        var deadLetterMessage = await _repository.GetByIdAsync(messageId);
        if (deadLetterMessage == null)
            throw new NotFoundException($"Dead letter message {messageId} not found");

        try
        {
            // Determine message type and deserialize
            var messageType = Type.GetType(deadLetterMessage.MessageType);
            var message = JsonSerializer.Deserialize(deadLetterMessage.MessageContent, messageType);

            // Republish message
            await _eventDispatcher.PublishAsync(message);

            // Remove from dead letter queue
            await _repository.DeleteAsync(messageId);

            _logger.LogInformation("Successfully reprocessed dead letter message {MessageId}", messageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reprocess dead letter message {MessageId}", messageId);
            throw;
        }
    }
}

public class DeadLetterMessage
{
    public string Id { get; set; }
    public string MessageType { get; set; }
    public string MessageContent { get; set; }
    public string Reason { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

## Advanced Features

### 1. Message Batching

Implement message batching for performance:

```csharp
public class BatchEventDispatcher : IEventDispatcher
{
    private readonly IEventDispatcher _innerDispatcher;
    private readonly ConcurrentQueue<object> _messageQueue;
    private readonly Timer _flushTimer;
    private readonly int _batchSize;
    private readonly ILogger<BatchEventDispatcher> _logger;

    public BatchEventDispatcher(
        IEventDispatcher innerDispatcher,
        int batchSize = 100,
        TimeSpan? flushInterval = null,
        ILogger<BatchEventDispatcher> logger = null)
    {
        _innerDispatcher = innerDispatcher;
        _messageQueue = new ConcurrentQueue<object>();
        _batchSize = batchSize;
        _logger = logger;

        var interval = flushInterval ?? TimeSpan.FromSeconds(5);
        _flushTimer = new Timer(FlushMessages, null, interval, interval);
    }

    public async Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default) where T : class, IEvent
    {
        _messageQueue.Enqueue(@event);

        if (_messageQueue.Count >= _batchSize)
        {
            await FlushMessagesAsync();
        }
    }

    private async void FlushMessages(object state)
    {
        await FlushMessagesAsync();
    }

    private async Task FlushMessagesAsync()
    {
        var messages = new List<object>();

        while (messages.Count < _batchSize && _messageQueue.TryDequeue(out var message))
        {
            messages.Add(message);
        }

        if (messages.Any())
        {
            _logger?.LogDebug("Flushing {Count} messages", messages.Count);

            foreach (var message in messages)
            {
                await _innerDispatcher.PublishAsync(message);
            }
        }
    }
}
```

### 2. Message Filtering and Routing

Implement message filtering and routing:

```csharp
public interface IMessageFilter
{
    bool ShouldProcess<T>(T message) where T : class;
}

public class TenantMessageFilter : IMessageFilter
{
    private readonly ITenantContext _tenantContext;

    public TenantMessageFilter(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public bool ShouldProcess<T>(T message) where T : class
    {
        if (message is ITenantAware tenantAware)
        {
            return tenantAware.TenantId == _tenantContext.TenantId;
        }

        return true; // Process non-tenant messages
    }
}

public class FilteringEventHandler<TEvent> : IEventHandler<TEvent>
    where TEvent : class, IEvent
{
    private readonly IEventHandler<TEvent> _handler;
    private readonly IEnumerable<IMessageFilter> _filters;

    public FilteringEventHandler(IEventHandler<TEvent> handler, IEnumerable<IMessageFilter> filters)
    {
        _handler = handler;
        _filters = filters;
    }

    public async Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default)
    {
        foreach (var filter in _filters)
        {
            if (!filter.ShouldProcess(@event))
            {
                return; // Skip processing
            }
        }

        await _handler.HandleAsync(@event, cancellationToken);
    }
}
```

## Configuration Options

### Message Broker Settings

```csharp
public class MessageBrokerOptions
{
    public string Type { get; set; } = "rabbitmq";
    public string ConnectionString { get; set; }
    public bool EnableRetries { get; set; } = true;
    public int MaxRetries { get; set; } = 3;
    public TimeSpan RetryInterval { get; set; } = TimeSpan.FromSeconds(5);
    public bool EnableDeadLetter { get; set; } = true;
    public string DeadLetterExchange { get; set; } = "dead-letter";
}
```

## API Reference

### IEventDispatcher Interface

```csharp
public interface IEventDispatcher
{
    Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default) where T : class, IEvent;
    Task PublishAsync<T>(T @event, string routingKey, CancellationToken cancellationToken = default) where T : class, IEvent;
}
```

### IEventHandler Interface

```csharp
public interface IEventHandler<in TEvent> where TEvent : class, IEvent
{
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
}
```

### Extension Methods

```csharp
public static class ConveyExtensions
{
    public static IConveyBuilder AddInMemoryEventDispatcher(this IConveyBuilder builder);
}

// From Convey.CQRS.Commands package:
public static IConveyBuilder AddCommandHandlers(this IConveyBuilder builder, Assembly assembly = null);

// From Convey.CQRS.Events package:
public static IConveyBuilder AddEventHandlers(this IConveyBuilder builder, Assembly assembly = null);
```

## Best Practices

1. **Use event sourcing patterns** - Design events as immutable facts
2. **Implement idempotent handlers** - Ensure handlers can safely process duplicate messages
3. **Handle failures gracefully** - Implement proper retry and dead letter strategies
4. **Use correlation IDs** - Track message flows across services
5. **Version your events** - Plan for event schema evolution
6. **Monitor message processing** - Track throughput, errors, and latency
7. **Implement circuit breakers** - Prevent cascade failures in event processing
8. **Use appropriate serialization** - Choose between JSON and binary based on requirements

## Troubleshooting

### Common Issues

1. **Messages not being processed**
   - Check message broker connectivity
   - Verify handler registration
   - Check message routing configuration

2. **Duplicate message processing**
   - Implement idempotent message handlers
   - Check for proper message acknowledgment
   - Verify exactly-once delivery semantics

3. **Performance issues**
   - Implement message batching
   - Use async processing patterns
   - Monitor queue depths and processing rates

4. **Dead letter queue buildup**
   - Review error handling logic
   - Implement dead letter message reprocessing
   - Monitor and alert on dead letter queue growth

