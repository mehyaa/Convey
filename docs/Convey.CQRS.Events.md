# Convey.CQRS.Events

Event handling abstractions for CQRS pattern providing event dispatching, handling, and integration with message brokers for building event-driven architectures.

## Installation

```bash
dotnet add package Convey.CQRS.Events
```

## Overview

Convey.CQRS.Events provides:
- **Event abstractions** - Interfaces for events and event handlers
- **Event dispatcher** - In-memory event dispatching
- **Automatic handler registration** - Convention-based handler discovery
- **Decorator support** - Event pipeline decorators
- **Integration support** - Works seamlessly with message brokers
- **Cancellation support** - Built-in cancellation token support

## Configuration

### Basic Setup

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddEventHandlers()
    .AddInMemoryEventDispatcher();

var app = builder.Build();
app.Run();
```

## Key Concepts

### 1. Events

Events represent things that have happened and implement the `IEvent` marker interface:

```csharp
public class UserCreatedEvent : IEvent
{
    public Guid UserId { get; }
    public string Name { get; }
    public string Email { get; }
    public string Role { get; }
    public DateTime CreatedAt { get; }

    public UserCreatedEvent(Guid userId, string name, string email, string role)
    {
        UserId = userId;
        Name = name;
        Email = email;
        Role = role;
        CreatedAt = DateTime.UtcNow;
    }
}

public class OrderCompletedEvent : IEvent
{
    public Guid OrderId { get; }
    public Guid CustomerId { get; }
    public decimal TotalAmount { get; }
    public IReadOnlyList<OrderItem> Items { get; }
    public DateTime CompletedAt { get; }

    public OrderCompletedEvent(Guid orderId, Guid customerId, decimal totalAmount,
        IReadOnlyList<OrderItem> items)
    {
        OrderId = orderId;
        CustomerId = customerId;
        TotalAmount = totalAmount;
        Items = items;
        CompletedAt = DateTime.UtcNow;
    }
}

public class PaymentProcessedEvent : IEvent
{
    public Guid PaymentId { get; }
    public Guid OrderId { get; }
    public decimal Amount { get; }
    public string Status { get; }
    public string TransactionId { get; }

    public PaymentProcessedEvent(Guid paymentId, Guid orderId, decimal amount,
        string status, string transactionId)
    {
        PaymentId = paymentId;
        OrderId = orderId;
        Amount = amount;
        Status = status;
        TransactionId = transactionId;
    }
}
```

### 2. Event Handlers

Event handlers implement business logic for events:

```csharp
public class UserCreatedEventHandler : IEventHandler<UserCreatedEvent>
{
    private readonly IEmailService _emailService;
    private readonly IAnalyticsService _analyticsService;
    private readonly ILogger<UserCreatedEventHandler> _logger;

    public UserCreatedEventHandler(
        IEmailService emailService,
        IAnalyticsService analyticsService,
        ILogger<UserCreatedEventHandler> logger)
    {
        _emailService = emailService;
        _analyticsService = analyticsService;
        _logger = logger;
    }

    public async Task HandleAsync(UserCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Handling UserCreatedEvent for user {UserId}", @event.UserId);

        try
        {
            // Send welcome email
            await _emailService.SendWelcomeEmailAsync(@event.Email, @event.Name);

            // Track analytics
            await _analyticsService.TrackUserRegistrationAsync(@event.UserId, @event.CreatedAt);

            _logger.LogInformation("Successfully processed UserCreatedEvent for user {UserId}", @event.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing UserCreatedEvent for user {UserId}", @event.UserId);
            throw; // Re-throw to trigger retry mechanism if configured
        }
    }
}

// Multiple handlers for the same event
public class UserNotificationEventHandler : IEventHandler<UserCreatedEvent>
{
    private readonly INotificationService _notificationService;

    public UserNotificationEventHandler(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public async Task HandleAsync(UserCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        await _notificationService.SendAdminNotificationAsync(
            $"New user registered: {@@event.Name} ({@@event.Email})");
    }
}

public class OrderCompletedEventHandler : IEventHandler<OrderCompletedEvent>
{
    private readonly IInventoryService _inventoryService;
    private readonly IShippingService _shippingService;
    private readonly IInvoiceService _invoiceService;

    public OrderCompletedEventHandler(
        IInventoryService inventoryService,
        IShippingService shippingService,
        IInvoiceService invoiceService)
    {
        _inventoryService = inventoryService;
        _shippingService = shippingService;
        _invoiceService = invoiceService;
    }

    public async Task HandleAsync(OrderCompletedEvent @event, CancellationToken cancellationToken = default)
    {
        // Update inventory
        await _inventoryService.UpdateStockAsync(@event.Items);

        // Create shipping request
        await _shippingService.CreateShippingRequestAsync(@event.OrderId, @event.CustomerId);

        // Generate invoice
        await _invoiceService.GenerateInvoiceAsync(@event.OrderId, @event.TotalAmount);
    }
}
```

### 3. Event Dispatcher

The event dispatcher routes events to their respective handlers:

```csharp
public class OrderService
{
    private readonly IEventDispatcher _eventDispatcher;
    private readonly IOrderRepository _orderRepository;

    public OrderService(IEventDispatcher eventDispatcher, IOrderRepository orderRepository)
    {
        _eventDispatcher = eventDispatcher;
        _orderRepository = orderRepository;
    }

    public async Task CompleteOrderAsync(Guid orderId)
    {
        var order = await _orderRepository.GetAsync(orderId);
        if (order == null)
        {
            throw new OrderNotFoundException(orderId);
        }

        // Update order status
        order.MarkAsCompleted();
        await _orderRepository.UpdateAsync(order);

        // Dispatch event
        var @event = new OrderCompletedEvent(
            order.Id,
            order.CustomerId,
            order.TotalAmount,
            order.Items);

        await _eventDispatcher.PublishAsync(@event);
    }
}
```

## Advanced Features

### 1. Event Decorators

Create event pipeline decorators for cross-cutting concerns:

```csharp
[Decorator]
public class LoggingEventHandlerDecorator<T> : IEventHandler<T> where T : class, IEvent
{
    private readonly IEventHandler<T> _handler;
    private readonly ILogger<LoggingEventHandlerDecorator<T>> _logger;

    public LoggingEventHandlerDecorator(
        IEventHandler<T> handler,
        ILogger<LoggingEventHandlerDecorator<T>> logger)
    {
        _handler = handler;
        _logger = logger;
    }

    public async Task HandleAsync(T @event, CancellationToken cancellationToken = default)
    {
        var eventName = typeof(T).Name;
        _logger.LogInformation("Handling event {EventName}: {@Event}", eventName, @event);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            await _handler.HandleAsync(@event, cancellationToken);
            stopwatch.Stop();

            _logger.LogInformation("Event {EventName} handled successfully in {ElapsedMs}ms",
                eventName, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error handling event {EventName} after {ElapsedMs}ms",
                eventName, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}

[Decorator]
public class RetryEventHandlerDecorator<T> : IEventHandler<T> where T : class, IEvent
{
    private readonly IEventHandler<T> _handler;
    private readonly ILogger<RetryEventHandlerDecorator<T>> _logger;
    private readonly int _maxRetries;
    private readonly TimeSpan _retryDelay;

    public RetryEventHandlerDecorator(
        IEventHandler<T> handler,
        ILogger<RetryEventHandlerDecorator<T>> logger,
        int maxRetries = 3,
        TimeSpan? retryDelay = null)
    {
        _handler = handler;
        _logger = logger;
        _maxRetries = maxRetries;
        _retryDelay = retryDelay ?? TimeSpan.FromSeconds(1);
    }

    public async Task HandleAsync(T @event, CancellationToken cancellationToken = default)
    {
        var attempt = 0;
        while (attempt <= _maxRetries)
        {
            try
            {
                await _handler.HandleAsync(@event, cancellationToken);
                return;
            }
            catch (Exception ex) when (attempt < _maxRetries && IsRetriableException(ex))
            {
                attempt++;
                _logger.LogWarning(ex, "Event handling failed (attempt {Attempt}/{MaxAttempts}), retrying in {Delay}ms",
                    attempt, _maxRetries + 1, _retryDelay.TotalMilliseconds);

                await Task.Delay(_retryDelay, cancellationToken);
            }
        }
    }

    private bool IsRetriableException(Exception ex)
    {
        return ex is not ArgumentException and not InvalidOperationException;
    }
}

[Decorator]
public class MetricsEventHandlerDecorator<T> : IEventHandler<T> where T : class, IEvent
{
    private readonly IEventHandler<T> _handler;
    private readonly IMetrics _metrics;

    public MetricsEventHandlerDecorator(IEventHandler<T> handler, IMetrics metrics)
    {
        _handler = handler;
        _metrics = metrics;
    }

    public async Task HandleAsync(T @event, CancellationToken cancellationToken = default)
    {
        var eventName = typeof(T).Name;
        using var timer = _metrics.Measure.Timer.Time($"event_handler_duration", new MetricTags("event", eventName));

        try
        {
            await _handler.HandleAsync(@event, cancellationToken);
            _metrics.Measure.Counter.Increment($"event_handler_success", new MetricTags("event", eventName));
        }
        catch
        {
            _metrics.Measure.Counter.Increment($"event_handler_error", new MetricTags("event", eventName));
            throw;
        }
    }
}
```

### 2. Event Sourcing Pattern

Implement event sourcing with event handlers:

```csharp
public abstract class AggregateRoot
{
    private readonly List<IEvent> _events = new();

    public IReadOnlyList<IEvent> Events => _events.AsReadOnly();

    protected void AddEvent(IEvent @event)
    {
        _events.Add(@event);
    }

    public void ClearEvents()
    {
        _events.Clear();
    }
}

public class Order : AggregateRoot
{
    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }
    public List<OrderItem> Items { get; private set; } = new();
    public decimal TotalAmount => Items.Sum(x => x.Price * x.Quantity);

    public Order(Guid customerId, List<OrderItem> items)
    {
        Id = Guid.NewGuid();
        CustomerId = customerId;
        Items = items;
        Status = OrderStatus.Created;

        AddEvent(new OrderCreatedEvent(Id, CustomerId, Items));
    }

    public void AddItem(OrderItem item)
    {
        Items.Add(item);
        AddEvent(new OrderItemAddedEvent(Id, item));
    }

    public void CompleteOrder()
    {
        if (Status != OrderStatus.Created)
        {
            throw new InvalidOperationException("Order is not in created status");
        }

        Status = OrderStatus.Completed;
        AddEvent(new OrderCompletedEvent(Id, CustomerId, TotalAmount, Items));
    }
}

public class OrderEventHandler :
    IEventHandler<OrderCreatedEvent>,
    IEventHandler<OrderItemAddedEvent>,
    IEventHandler<OrderCompletedEvent>
{
    private readonly IEventStore _eventStore;

    public OrderEventHandler(IEventStore eventStore)
    {
        _eventStore = eventStore;
    }

    public async Task HandleAsync(OrderCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        await _eventStore.SaveEventAsync(@event);
    }

    public async Task HandleAsync(OrderItemAddedEvent @event, CancellationToken cancellationToken = default)
    {
        await _eventStore.SaveEventAsync(@event);
    }

    public async Task HandleAsync(OrderCompletedEvent @event, CancellationToken cancellationToken = default)
    {
        await _eventStore.SaveEventAsync(@event);
    }
}
```

### 3. Integration with Message Brokers

Publish events to external systems:

```csharp
[Decorator]
public class MessageBrokerEventHandlerDecorator<T> : IEventHandler<T> where T : class, IEvent
{
    private readonly IEventHandler<T> _handler;
    private readonly IBusPublisher _publisher;

    public MessageBrokerEventHandlerDecorator(IEventHandler<T> handler, IBusPublisher publisher)
    {
        _handler = handler;
        _publisher = publisher;
    }

    public async Task HandleAsync(T @event, CancellationToken cancellationToken = default)
    {
        // Handle locally first
        await _handler.HandleAsync(@event, cancellationToken);

        // Then publish to message broker for other services
        await _publisher.PublishAsync(@event);
    }
}

// Or create specific integration handlers
public class ExternalEventPublisher :
    IEventHandler<UserCreatedEvent>,
    IEventHandler<OrderCompletedEvent>
{
    private readonly IBusPublisher _publisher;

    public ExternalEventPublisher(IBusPublisher publisher)
    {
        _publisher = publisher;
    }

    public async Task HandleAsync(UserCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        // Transform internal event to external contract if needed
        var externalEvent = new UserRegisteredEvent
        {
            UserId = @event.UserId,
            Email = @event.Email,
            RegisteredAt = @event.CreatedAt
        };

        await _publisher.PublishAsync(externalEvent);
    }

    public async Task HandleAsync(OrderCompletedEvent @event, CancellationToken cancellationToken = default)
    {
        await _publisher.PublishAsync(@event); // Direct publishing
    }
}
```

## API Reference

### Interfaces

#### IEvent
```csharp
public interface IEvent
{
    // Marker interface - no members
}
```

Marker interface that all events must implement.

#### IEventHandler&lt;T&gt;
```csharp
public interface IEventHandler<in TEvent> where TEvent : class, IEvent
{
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
}
```

Interface for event handlers.

**Type Parameters:**
- `TEvent` - Type of event to handle

**Methods:**
- `HandleAsync()` - Handles the event asynchronously

#### IEventDispatcher
```csharp
public interface IEventDispatcher
{
    Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default) where T : class, IEvent;
}
```

Interface for dispatching events to their handlers.

**Methods:**
- `PublishAsync<T>()` - Publishes an event to all registered handlers

### Extension Methods

#### AddEventHandlers()
```csharp
public static IConveyBuilder AddEventHandlers(this IConveyBuilder builder)
```

Registers all event handlers found in loaded assemblies.

#### AddInMemoryEventDispatcher()
```csharp
public static IConveyBuilder AddInMemoryEventDispatcher(this IConveyBuilder builder)
```

Registers the in-memory event dispatcher implementation.

## Usage Examples

### Basic Event Flow

```csharp
// 1. Define events
public class ProductCreatedEvent : IEvent
{
    public Guid ProductId { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
}

// 2. Create handlers
public class ProductCreatedEventHandler : IEventHandler<ProductCreatedEvent>
{
    private readonly ISearchIndexService _searchService;
    private readonly ICacheService _cacheService;

    public ProductCreatedEventHandler(ISearchIndexService searchService, ICacheService cacheService)
    {
        _searchService = searchService;
        _cacheService = cacheService;
    }

    public async Task HandleAsync(ProductCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        // Update search index
        await _searchService.IndexProductAsync(@event.ProductId, @event.Name);

        // Invalidate cache
        await _cacheService.InvalidateAsync($"products");
    }
}

// 3. Use in service
public class ProductService
{
    private readonly IEventDispatcher _eventDispatcher;
    private readonly IProductRepository _repository;

    public ProductService(IEventDispatcher eventDispatcher, IProductRepository repository)
    {
        _eventDispatcher = eventDispatcher;
        _repository = repository;
    }

    public async Task CreateProductAsync(CreateProductCommand command)
    {
        var product = new Product(command.Name, command.Price);
        await _repository.AddAsync(product);

        var @event = new ProductCreatedEvent
        {
            ProductId = product.Id,
            Name = product.Name,
            Price = product.Price
        };

        await _eventDispatcher.PublishAsync(@event);
    }
}
```

### Event-Driven Workflow

```csharp
// Order processing workflow
public class OrderWorkflowEvents
{
    public class OrderSubmittedEvent : IEvent
    {
        public Guid OrderId { get; set; }
        public List<OrderItem> Items { get; set; }
    }

    public class InventoryReservedEvent : IEvent
    {
        public Guid OrderId { get; set; }
        public List<ReservationItem> Reservations { get; set; }
    }

    public class PaymentProcessedEvent : IEvent
    {
        public Guid OrderId { get; set; }
        public decimal Amount { get; set; }
        public bool Success { get; set; }
    }

    public class OrderFulfilledEvent : IEvent
    {
        public Guid OrderId { get; set; }
        public string TrackingNumber { get; set; }
    }
}

// Workflow handlers
public class OrderWorkflowHandler :
    IEventHandler<OrderSubmittedEvent>,
    IEventHandler<InventoryReservedEvent>,
    IEventHandler<PaymentProcessedEvent>
{
    private readonly IEventDispatcher _eventDispatcher;
    private readonly IInventoryService _inventoryService;
    private readonly IPaymentService _paymentService;
    private readonly IShippingService _shippingService;

    public OrderWorkflowHandler(
        IEventDispatcher eventDispatcher,
        IInventoryService inventoryService,
        IPaymentService paymentService,
        IShippingService shippingService)
    {
        _eventDispatcher = eventDispatcher;
        _inventoryService = inventoryService;
        _paymentService = paymentService;
        _shippingService = shippingService;
    }

    public async Task HandleAsync(OrderSubmittedEvent @event, CancellationToken cancellationToken = default)
    {
        // Reserve inventory
        var reservations = await _inventoryService.ReserveAsync(@event.Items);

        await _eventDispatcher.PublishAsync(new InventoryReservedEvent
        {
            OrderId = @event.OrderId,
            Reservations = reservations
        });
    }

    public async Task HandleAsync(InventoryReservedEvent @event, CancellationToken cancellationToken = default)
    {
        // Process payment
        var totalAmount = @event.Reservations.Sum(x => x.Price * x.Quantity);
        var paymentResult = await _paymentService.ProcessAsync(@event.OrderId, totalAmount);

        await _eventDispatcher.PublishAsync(new PaymentProcessedEvent
        {
            OrderId = @event.OrderId,
            Amount = totalAmount,
            Success = paymentResult.Success
        });
    }

    public async Task HandleAsync(PaymentProcessedEvent @event, CancellationToken cancellationToken = default)
    {
        if (@event.Success)
        {
            // Fulfill order
            var trackingNumber = await _shippingService.CreateShipmentAsync(@event.OrderId);

            await _eventDispatcher.PublishAsync(new OrderFulfilledEvent
            {
                OrderId = @event.OrderId,
                TrackingNumber = trackingNumber
            });
        }
        else
        {
            // Release inventory
            await _inventoryService.ReleaseReservationAsync(@event.OrderId);
        }
    }
}
```

## Testing

### Unit Testing Event Handlers

```csharp
public class UserCreatedEventHandlerTests
{
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly Mock<IAnalyticsService> _analyticsServiceMock;
    private readonly UserCreatedEventHandler _handler;

    public UserCreatedEventHandlerTests()
    {
        _emailServiceMock = new Mock<IEmailService>();
        _analyticsServiceMock = new Mock<IAnalyticsService>();
        _handler = new UserCreatedEventHandler(
            _emailServiceMock.Object,
            _analyticsServiceMock.Object,
            Mock.Of<ILogger<UserCreatedEventHandler>>());
    }

    [Fact]
    public async Task HandleAsync_ValidEvent_SendsEmailAndTracksAnalytics()
    {
        // Arrange
        var @event = new UserCreatedEvent(Guid.NewGuid(), "John Doe", "john@example.com", "User");

        // Act
        await _handler.HandleAsync(@event);

        // Assert
        _emailServiceMock.Verify(x => x.SendWelcomeEmailAsync(@event.Email, @event.Name), Times.Once);
        _analyticsServiceMock.Verify(x => x.TrackUserRegistrationAsync(@event.UserId, @event.CreatedAt), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_EmailServiceFails_PropagatesException()
    {
        // Arrange
        var @event = new UserCreatedEvent(Guid.NewGuid(), "John Doe", "john@example.com", "User");
        _emailServiceMock.Setup(x => x.SendWelcomeEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Email service unavailable"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.HandleAsync(@event));
    }
}
```

### Integration Testing

```csharp
public class EventDispatcherIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly IEventDispatcher _dispatcher;

    public EventDispatcherIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _dispatcher = _factory.Services.GetRequiredService<IEventDispatcher>();
    }

    [Fact]
    public async Task PublishAsync_ValidEvent_HandlersExecuted()
    {
        // Arrange
        var @event = new TestEvent { Id = Guid.NewGuid(), Message = "Test message" };
        var handler = _factory.Services.GetRequiredService<TestEventHandler>();

        // Act
        await _dispatcher.PublishAsync(@event);

        // Assert
        await WaitForEventProcessing();
        Assert.True(handler.EventReceived);
        Assert.Equal(@event.Id, handler.ReceivedEvent.Id);
    }

    private async Task WaitForEventProcessing()
    {
        // Wait for async event processing to complete
        await Task.Delay(100);
    }
}
```

## Best Practices

1. **Keep events immutable** - Events should be immutable after creation
2. **Use meaningful event names** - Event names should clearly describe what happened
3. **Include relevant data** - Events should contain all necessary information
4. **Handle failures gracefully** - Implement proper error handling and retry logic
5. **Consider event ordering** - Be aware of event processing order when needed
6. **Version your events** - Plan for event schema evolution
7. **Keep handlers focused** - Each handler should have a single responsibility
8. **Use decorators for cross-cutting concerns** - Logging, metrics, retries, etc.

## Troubleshooting

### Common Issues

1. **Handler not found**
   - Ensure handlers implement `IEventHandler<T>`
   - Check that `AddEventHandlers()` is called
   - Verify handler is not marked with `[Decorator]` attribute

2. **Events not being processed**
   - Check event dispatcher registration
   - Verify event type matches handler type exactly
   - Ensure proper dependency injection setup

3. **Multiple handlers causing issues**
   - Remember that all handlers for an event will be executed
   - Consider using different events for different purposes
   - Implement proper error isolation between handlers

4. **Performance issues**
   - Consider async processing for long-running handlers
   - Implement proper exception handling to avoid blocking
   - Use decorators for metrics and monitoring
