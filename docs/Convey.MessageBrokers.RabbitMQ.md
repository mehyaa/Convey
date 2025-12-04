---
layout: default
title: Convey.MessageBrokers.RabbitMQ
parent: Message Brokers
---
# Convey.MessageBrokers.RabbitMQ

Comprehensive RabbitMQ integration for building event-driven microservices with automatic message routing, retry mechanisms, dead letter handling, and extensive configuration options.

## Installation

```bash
dotnet add package Convey.MessageBrokers.RabbitMQ
```

## Overview

Convey.MessageBrokers.RabbitMQ provides:
- **Message publishing** - Reliable message publishing with confirmations
- **Message subscription** - Automatic message handling and routing
- **Convention-based routing** - Automatic exchange and queue configuration
- **Retry mechanisms** - Built-in retry policies for failed messages
- **Dead letter handling** - Automatic dead letter queue setup
- **Context propagation** - Correlation and tracing context passing
- **SSL/TLS support** - Secure connections with certificate validation
- **Plugin architecture** - Extensible message processing pipeline

## Configuration

### Basic Setup

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddRabbitMq()
    .AddCommandHandlers() // Register command handlers (from Convey.CQRS.Commands)
    .AddEventHandlers(); // Register event handlers (from Convey.CQRS.Events)

var app = builder.Build();
app.UseRabbitMq();
app.Run();
```

### RabbitMQ Options

Configure in `appsettings.json`:

```json
{
  "rabbitmq": {
    "connectionName": "my-service",
    "hostNames": ["localhost"],
    "port": 5672,
    "virtualHost": "/",
    "username": "guest",
    "password": "guest",
    "requestedHeartbeat": "00:01:00",
    "requestedConnectionTimeout": "00:00:30",
    "messageProcessingTimeout": "00:05:00",
    "retries": 3,
    "retryInterval": 1000,
    "messagesPersisted": true,
    "requeueFailedMessages": false,
    "conventions": {
      "casing": "snakeCase"
    },
    "exchange": {
      "declare": true,
      "durable": true,
      "autoDelete": false,
      "type": "topic"
    },
    "queue": {
      "declare": true,
      "durable": true,
      "exclusive": false,
      "autoDelete": false
    },
    "qos": {
      "prefetchSize": 0,
      "prefetchCount": 10,
      "global": false
    },
    "deadLetter": {
      "enabled": true,
      "prefix": "dlx",
      "ttl": 86400000
    }
  }
}
```

## Key Features

### 1. Event Publishing

```csharp
public class UserCreatedEvent : IEvent
{
    public Guid UserId { get; }
    public string Name { get; }
    public string Email { get; }
    public DateTime CreatedAt { get; }

    public UserCreatedEvent(Guid userId, string name, string email)
    {
        UserId = userId;
        Name = name;
        Email = email;
        CreatedAt = DateTime.UtcNow;
    }
}

public class UserService
{
    private readonly IBusPublisher _publisher;

    public UserService(IBusPublisher publisher)
    {
        _publisher = publisher;
    }

    public async Task CreateUserAsync(CreateUserCommand command)
    {
        // Create user logic
        var user = new User(command.Name, command.Email);
        await userRepository.AddAsync(user);

        // Publish event
        var @event = new UserCreatedEvent(user.Id, user.Name, user.Email);
        await _publisher.PublishAsync(@event);
    }
}
```

### 2. Event Handling

```csharp
public class UserCreatedEventHandler : IEventHandler<UserCreatedEvent>
{
    private readonly IEmailService _emailService;
    private readonly ILogger<UserCreatedEventHandler> _logger;

    public UserCreatedEventHandler(IEmailService emailService, ILogger<UserCreatedEventHandler> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    public async Task HandleAsync(UserCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Handling UserCreatedEvent for user {UserId}", @event.UserId);

        try
        {
            await _emailService.SendWelcomeEmailAsync(@event.Email, @event.Name);
            _logger.LogInformation("Welcome email sent to {Email}", @event.Email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send welcome email to {Email}", @event.Email);
            throw; // This will trigger retry mechanism
        }
    }
}

// Multiple handlers for the same event
public class UserAnalyticsEventHandler : IEventHandler<UserCreatedEvent>
{
    private readonly IAnalyticsService _analyticsService;

    public UserAnalyticsEventHandler(IAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    public async Task HandleAsync(UserCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        await _analyticsService.TrackUserRegistrationAsync(@event.UserId, @event.CreatedAt);
    }
}
```

### 3. Message Routing Conventions

```csharp
// Automatic routing based on message type
public class OrderCreatedEvent : IEvent
{
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public decimal TotalAmount { get; set; }
}

// Will automatically route to:
// Exchange: order_created_event (based on class name)
// Queue: order_service.order_created_event (based on service name + event name)
// Routing Key: order_created_event

// Custom routing with attributes
[Message("orders", "order.created", "order-processing-queue")]
public class CustomOrderEvent : IEvent
{
    public Guid OrderId { get; set; }
    public string Status { get; set; }
}
```

### 4. Advanced Message Publishing

```csharp
public class OrderService
{
    private readonly IBusPublisher _publisher;

    public OrderService(IBusPublisher publisher)
    {
        _publisher = publisher;
    }

    public async Task ProcessOrderAsync(Order order)
    {
        // Publish with custom routing
        await _publisher.PublishAsync(
            new OrderProcessedEvent(order.Id, order.Status),
            routingKey: "order.processed.high-priority"
        );

        // Publish with message properties
        await _publisher.PublishAsync(
            new OrderShippedEvent(order.Id, order.TrackingNumber),
            messageId: Guid.NewGuid().ToString(),
            correlationId: order.CorrelationId,
            timestamp: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            headers: new Dictionary<string, object>
            {
                { "source", "order-service" },
                { "version", "1.0" },
                { "priority", "high" }
            }
        );

        // Delayed publishing (if DelayedExchange plugin is enabled)
        await _publisher.PublishAsync(
            new OrderReminderEvent(order.Id),
            delay: TimeSpan.FromHours(24)
        );
    }
}
```

### 5. Error Handling and Retries

```csharp
public class PaymentEventHandler : IEventHandler<PaymentProcessedEvent>
{
    private readonly IPaymentService _paymentService;
    private readonly ILogger<PaymentEventHandler> _logger;

    public PaymentEventHandler(IPaymentService paymentService, ILogger<PaymentEventHandler> logger)
    {
        _paymentService = paymentService;
        _logger = logger;
    }

    public async Task HandleAsync(PaymentProcessedEvent @event, CancellationToken cancellationToken = default)
    {
        try
        {
            await _paymentService.UpdatePaymentStatusAsync(@event.PaymentId, @event.Status);
        }
        catch (TransientException ex)
        {
            _logger.LogWarning(ex, "Transient error processing payment {PaymentId}, will retry", @event.PaymentId);
            throw; // Will be retried according to retry policy
        }
        catch (PermanentException ex)
        {
            _logger.LogError(ex, "Permanent error processing payment {PaymentId}, moving to DLQ", @event.PaymentId);
            throw new RejectException("Permanent failure", ex); // Will be moved to dead letter queue
        }
    }
}

// Custom exception handling
public class CustomExceptionToMessageMapper : IExceptionToMessageMapper
{
    public object Map(Exception exception, object message)
    {
        return new FailedMessage
        {
            Message = message,
            Error = exception.Message,
            StackTrace = exception.StackTrace,
            Timestamp = DateTime.UtcNow,
            CanRetry = exception is not PermanentException
        };
    }
}
```

## Advanced Configuration

### 1. SSL/TLS Configuration

```json
{
  "rabbitmq": {
    "ssl": {
      "enabled": true,
      "serverName": "rabbitmq.example.com",
      "certificatePath": "/path/to/certificate.pfx",
      "certificatePassword": "password",
      "acceptablePolicyErrors": ["RemoteCertificateNotAvailable"],
      "checkCertificateRevocation": true
    }
  }
}
```

### 2. High Availability Setup

```json
{
  "rabbitmq": {
    "hostNames": ["rabbit1.example.com", "rabbit2.example.com", "rabbit3.example.com"],
    "port": 5672,
    "networkRecoveryInterval": "00:00:05",
    "requestedHeartbeat": "00:01:00",
    "requestedConnectionTimeout": "00:00:30"
  }
}
```

### 3. Performance Tuning

```json
{
  "rabbitmq": {
    "qos": {
      "prefetchCount": 50,
      "prefetchSize": 0,
      "global": false
    },
    "maxProducerChannels": 10,
    "messageProcessingTimeout": "00:02:00"
  }
}
```

### 4. Plugin Integration

```csharp
builder.Services.AddConvey()
    .AddRabbitMq(plugins: registry => registry
        .Add<MessageTracingPlugin>()
        .Add<MessageDeduplicationPlugin>()
        .Add<MessageCompressionPlugin>()
    );

public class MessageTracingPlugin : RabbitMqPlugin
{
    public override async Task HandleAsync(object message, object correlationContext,
        Func<Task> next)
    {
        using var activity = StartActivity(message.GetType().Name);

        try
        {
            await next();
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}
```

## Message Patterns

### 1. Request-Response Pattern

```csharp
// Request
public class GetUserQuery : IRequest<UserDto>
{
    public Guid UserId { get; set; }
}

// Response
public class UserDto
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
}

// Handler
public class GetUserQueryHandler : IRequestHandler<GetUserQuery, UserDto>
{
    private readonly IUserRepository _userRepository;

    public GetUserQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<UserDto> HandleAsync(GetUserQuery query, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetAsync(query.UserId);
        return new UserDto
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email
        };
    }
}

// Usage
public class UserController : ControllerBase
{
    private readonly IBusPublisher _publisher;

    public UserController(IBusPublisher publisher)
    {
        _publisher = publisher;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserDto>> GetUser(Guid id)
    {
        var query = new GetUserQuery { UserId = id };
        var response = await _publisher.SendAsync<GetUserQuery, UserDto>(query);
        return Ok(response);
    }
}
```

### 2. Saga Pattern

```csharp
public class OrderSaga : ISaga<OrderCreatedEvent>,
                        ISaga<PaymentProcessedEvent>,
                        ISaga<InventoryReservedEvent>
{
    private readonly IBusPublisher _publisher;
    private readonly ISagaRepository _repository;

    public OrderSaga(IBusPublisher publisher, ISagaRepository repository)
    {
        _publisher = publisher;
        _repository = repository;
    }

    public async Task HandleAsync(OrderCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        var sagaData = new OrderSagaData
        {
            OrderId = @event.OrderId,
            CustomerId = @event.CustomerId,
            TotalAmount = @event.TotalAmount,
            Status = OrderSagaStatus.PaymentPending
        };

        await _repository.SaveAsync(sagaData);

        // Start payment process
        await _publisher.PublishAsync(new ProcessPaymentCommand
        {
            OrderId = @event.OrderId,
            Amount = @event.TotalAmount,
            CustomerId = @event.CustomerId
        });
    }

    public async Task HandleAsync(PaymentProcessedEvent @event, CancellationToken cancellationToken = default)
    {
        var sagaData = await _repository.GetAsync(@event.OrderId);

        if (@event.Success)
        {
            sagaData.Status = OrderSagaStatus.InventoryPending;
            await _repository.UpdateAsync(sagaData);

            await _publisher.PublishAsync(new ReserveInventoryCommand
            {
                OrderId = @event.OrderId,
                Items = sagaData.Items
            });
        }
        else
        {
            sagaData.Status = OrderSagaStatus.PaymentFailed;
            await _repository.UpdateAsync(sagaData);

            await _publisher.PublishAsync(new OrderFailedEvent
            {
                OrderId = @event.OrderId,
                Reason = "Payment failed"
            });
        }
    }

    // ... Handle other events
}
```

## Testing

### Unit Testing

```csharp
public class UserServiceTests
{
    private readonly Mock<IBusPublisher> _publisherMock;
    private readonly Mock<IUserRepository> _repositoryMock;
    private readonly UserService _userService;

    public UserServiceTests()
    {
        _publisherMock = new Mock<IBusPublisher>();
        _repositoryMock = new Mock<IUserRepository>();
        _userService = new UserService(_repositoryMock.Object, _publisherMock.Object);
    }

    [Fact]
    public async Task CreateUserAsync_ValidUser_PublishesEvent()
    {
        // Arrange
        var command = new CreateUserCommand("John Doe", "john@example.com");

        // Act
        await _userService.CreateUserAsync(command);

        // Assert
        _publisherMock.Verify(x => x.PublishAsync(
            It.Is<UserCreatedEvent>(e => e.Name == command.Name && e.Email == command.Email),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<object>(),
            It.IsAny<IDictionary<string, object>>()),
            Times.Once);
    }
}
```

### Integration Testing

```csharp
public class RabbitMqIntegrationTests : IClassFixture<RabbitMqTestFixture>
{
    private readonly RabbitMqTestFixture _fixture;
    private readonly IBusPublisher _publisher;
    private readonly TestEventHandler _handler;

    public RabbitMqIntegrationTests(RabbitMqTestFixture fixture)
    {
        _fixture = fixture;
        _publisher = fixture.GetService<IBusPublisher>();
        _handler = fixture.GetService<TestEventHandler>();
    }

    [Fact]
    public async Task PublishEvent_ValidEvent_HandlerReceivesMessage()
    {
        // Arrange
        var @event = new TestEvent { Id = Guid.NewGuid(), Message = "Test message" };

        // Act
        await _publisher.PublishAsync(@event);

        // Assert
        await _fixture.WaitForMessageAsync(TimeSpan.FromSeconds(5));
        Assert.True(_handler.MessageReceived);
        Assert.Equal(@event.Id, _handler.ReceivedEvent.Id);
    }
}

public class RabbitMqTestFixture : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly CancellationTokenSource _cancellationTokenSource;

    public RabbitMqTestFixture()
    {
        var services = new ServiceCollection();

        services.AddConvey()
            .AddRabbitMq(options =>
            {
                // Use test-specific configuration
                options.HostNames = new[] { "localhost" };
                options.VirtualHost = "/test";
                options.Queue.AutoDelete = true;
            })
            .AddCommandHandlers() // Register command handlers
            .AddEventHandlers(); // Register event handlers

        services.AddSingleton<TestEventHandler>();

        _serviceProvider = services.BuildServiceProvider();
        _cancellationTokenSource = new CancellationTokenSource();

        // Start message processing
        var app = _serviceProvider.GetRequiredService<IApplicationBuilder>();
        app.UseRabbitMq();
    }

    public async Task WaitForMessageAsync(TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        // Wait for message processing
        await Task.Delay(100, cts.Token);
    }

    public T GetService<T>() => _serviceProvider.GetRequiredService<T>();

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _serviceProvider?.Dispose();
    }
}
```

## Best Practices

1. **Use meaningful event names** - Event names should clearly describe what happened
2. **Keep events immutable** - Events should be immutable after creation
3. **Include correlation IDs** - For tracing across services
4. **Handle failures gracefully** - Implement proper retry and dead letter handling
5. **Use schemas** - Consider using schema registry for message evolution
6. **Monitor message flow** - Implement proper logging and metrics
7. **Design for idempotency** - Message handlers should be idempotent
8. **Version your messages** - Plan for message schema evolution

## Troubleshooting

### Common Issues

1. **Connection failures**
   - Check RabbitMQ server status
   - Verify connection string and credentials
   - Check network connectivity and firewall rules

2. **Messages not being consumed**
   - Verify queue bindings and routing keys
   - Check consumer registration
   - Ensure proper exchange configuration

3. **High memory usage**
   - Reduce prefetch count
   - Implement proper message acknowledgment
   - Check for message accumulation in queues

4. **Performance issues**
   - Tune QoS settings
   - Use connection pooling
   - Optimize message serialization

