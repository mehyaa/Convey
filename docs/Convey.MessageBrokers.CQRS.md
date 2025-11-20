# Convey.MessageBrokers.CQRS

Integration layer between Convey's message brokers and CQRS pattern, providing extension methods for publishing commands and events through message brokers and dispatchers that route CQRS messages to message bus.

## Installation

```bash
dotnet add package Convey.MessageBrokers.CQRS
```

## Overview

Convey.MessageBrokers.CQRS provides:
- **CQRS integration** - Bridge between CQRS dispatchers and message brokers
- **Command routing** - Route commands through message brokers via `ICommandDispatcher`
- **Event routing** - Route events through message brokers via `IEventDispatcher`
- **Message context** - Automatic correlation context propagation
- **Subscription helpers** - Simplified subscription setup for commands and events

## Configuration

### Basic Setup

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddCommandHandlers()
    .AddEventHandlers()
    .AddRabbitMq()
    .AddServiceBusCommandDispatcher()  // Use message broker for commands
    .AddServiceBusEventDispatcher();   // Use message broker for events

var app = builder.Build();

// Subscribe to commands and events from message broker
var busSubscriber = app.Services.GetRequiredService<IBusSubscriber>();
busSubscriber
    .SubscribeCommand<CreateUser>()
    .SubscribeCommand<UpdateUser>()
    .SubscribeEvent<UserCreated>()
    .SubscribeEvent<UserUpdated>();

app.Run();
```

### Using with Different Message Brokers

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddCommandHandlers()
    .AddEventHandlers()
    .AddRabbitMq(rabbit =>
    {
        rabbit.ConnectionName = "cqrs-service";
        rabbit.RequestRetries = 3;
        rabbit.RequestTimeout = TimeSpan.FromSeconds(30);
    })
    .AddServiceBusCommandDispatcher()
    .AddServiceBusEventDispatcher();

var app = builder.Build();
app.Run();
```

## Key Features

### 1. Command Dispatching via Message Broker

Route commands through message brokers instead of direct in-process handling:

```csharp
// Command definition (same as standard CQRS)
public class CreateUser : ICommand
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
}

// Command handler (same as standard CQRS)
public class CreateUserHandler : ICommandHandler<CreateUser>
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<CreateUserHandler> _logger;

    public CreateUserHandler(IUserRepository userRepository, ILogger<CreateUserHandler> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task HandleAsync(CreateUser command, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating user {UserId} with email {Email}", command.Id, command.Email);

        var user = new User
        {
            Id = command.Id,
            Email = command.Email,
            FirstName = command.FirstName,
            LastName = command.LastName,
            CreatedAt = DateTime.UtcNow
        };

        await _userRepository.CreateAsync(user);
        _logger.LogInformation("User {UserId} created successfully", command.Id);
    }
}

// Using the command dispatcher (routes through message broker)
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly ICommandDispatcher _commandDispatcher;

    public UsersController(ICommandDispatcher commandDispatcher)
    {
        _commandDispatcher = commandDispatcher;
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        var command = new CreateUser
        {
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName
        };

        // This will publish the command to the message broker
        // instead of handling it in-process
        await _commandDispatcher.SendAsync(command);

        return Accepted(new { Id = command.Id });
    }
}
```

### 2. Event Publishing via Message Broker

Route events through message brokers for distributed processing:

```csharp
// Event definition (same as standard CQRS)
public class UserCreated : IEvent
{
    public Guid Id { get; set; }
    public string Email { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public DateTime CreatedAt { get; set; }
}

// Event handler (same as standard CQRS)
public class UserCreatedHandler : IEventHandler<UserCreated>
{
    private readonly IEmailService _emailService;
    private readonly ILogger<UserCreatedHandler> _logger;

    public UserCreatedHandler(IEmailService emailService, ILogger<UserCreatedHandler> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    public async Task HandleAsync(UserCreated @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing UserCreated event for user {UserId}", @event.Id);

        await _emailService.SendWelcomeEmailAsync(@event.Email, @event.FirstName);

        _logger.LogInformation("Welcome email sent for user {UserId}", @event.Id);
    }
}

// Publishing events via event dispatcher
public class UserService
{
    private readonly IEventDispatcher _eventDispatcher;
    private readonly IUserRepository _userRepository;

    public UserService(IEventDispatcher eventDispatcher, IUserRepository userRepository)
    {
        _eventDispatcher = eventDispatcher;
        _userRepository = userRepository;
    }

    public async Task CreateUserAsync(CreateUser command)
    {
        var user = new User
        {
            Id = command.Id,
            Email = command.Email,
            FirstName = command.FirstName,
            LastName = command.LastName,
            CreatedAt = DateTime.UtcNow
        };

        await _userRepository.CreateAsync(user);

        // Publish event via message broker
        var userCreatedEvent = new UserCreated
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            CreatedAt = user.CreatedAt
        };

        await _eventDispatcher.PublishAsync(userCreatedEvent);
    }
}
```

### 3. Direct Message Broker Publishing with Context

Use extension methods for direct publishing with message context:

```csharp
public class OrderService
{
    private readonly IBusPublisher _busPublisher;
    private readonly ICorrelationContextAccessor _correlationContextAccessor;

    public OrderService(IBusPublisher busPublisher, ICorrelationContextAccessor correlationContextAccessor)
    {
        _busPublisher = busPublisher;
        _correlationContextAccessor = correlationContextAccessor;
    }

    public async Task ProcessOrderAsync(ProcessOrder command)
    {
        // Process the order...

        // Publish command with correlation context
        var fulfillOrderCommand = new FulfillOrder
        {
            OrderId = command.OrderId,
            Items = command.Items
        };

        await _busPublisher.SendAsync(fulfillOrderCommand, _correlationContextAccessor.CorrelationContext);

        // Publish event with correlation context
        var orderProcessedEvent = new OrderProcessed
        {
            OrderId = command.OrderId,
            ProcessedAt = DateTime.UtcNow
        };

        await _busPublisher.PublishAsync(orderProcessedEvent, _correlationContextAccessor.CorrelationContext);
    }
}
```

### 4. Message Subscription Setup

Set up subscribers to handle commands and events from message brokers:

```csharp
// In Program.cs or Startup.cs
var app = builder.Build();

var busSubscriber = app.Services.GetRequiredService<IBusSubscriber>();

// Subscribe to commands - these will be routed to ICommandHandler<T>
busSubscriber
    .SubscribeCommand<CreateUser>()
    .SubscribeCommand<UpdateUser>()
    .SubscribeCommand<DeleteUser>()
    .SubscribeCommand<ProcessOrder>()
    .SubscribeCommand<FulfillOrder>();

// Subscribe to events - these will be routed to IEventHandler<T>
busSubscriber
    .SubscribeEvent<UserCreated>()
    .SubscribeEvent<UserUpdated>()
    .SubscribeEvent<UserDeleted>()
    .SubscribeEvent<OrderProcessed>()
    .SubscribeEvent<OrderFulfilled>();

app.Run();
```

### 5. ServiceBusMessageDispatcher Implementation

The core dispatcher that bridges CQRS and message brokers:

```csharp
// This is provided by the package - shown for understanding
internal sealed class ServiceBusMessageDispatcher : ICommandDispatcher, IEventDispatcher
{
    private readonly IBusPublisher _busPublisher;
    private readonly ICorrelationContextAccessor _accessor;

    public ServiceBusMessageDispatcher(IBusPublisher busPublisher, ICorrelationContextAccessor accessor)
    {
        _busPublisher = busPublisher;
        _accessor = accessor;
    }

    public Task SendAsync<T>(T command, CancellationToken cancellationToken = default)
        where T : class, ICommand
        => _busPublisher.SendAsync(command, _accessor.CorrelationContext, cancellationToken: cancellationToken);

    public Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default)
        where T : class, IEvent
        => _busPublisher.PublishAsync(@event, _accessor.CorrelationContext, cancellationToken: cancellationToken);
}
```

## API Reference

### Extension Methods

```csharp
public static class Extensions
{
    // Publishing extensions for IBusPublisher
    public static Task SendAsync<TCommand>(
        this IBusPublisher busPublisher,
        TCommand command,
        object messageContext,
        CancellationToken cancellationToken = default)
        where TCommand : class, ICommand;

    public static Task PublishAsync<TEvent>(
        this IBusPublisher busPublisher,
        TEvent @event,
        object messageContext,
        CancellationToken cancellationToken = default)
        where TEvent : class, IEvent;

    // Subscription extensions for IBusSubscriber
    public static IBusSubscriber SubscribeCommand<T>(this IBusSubscriber busSubscriber)
        where T : class, ICommand;

    public static IBusSubscriber SubscribeEvent<T>(this IBusSubscriber busSubscriber)
        where T : class, IEvent;

    // Service registration extensions
    public static IConveyBuilder AddServiceBusCommandDispatcher(this IConveyBuilder builder);
    public static IConveyBuilder AddServiceBusEventDispatcher(this IConveyBuilder builder);
}
```

### Dependencies

This package requires:
- `Convey.MessageBrokers` - For `IBusPublisher` and `IBusSubscriber` interfaces
- `Convey.CQRS.Commands` - For `ICommand`, `ICommandHandler<T>`, and `ICommandDispatcher` interfaces
- `Convey.CQRS.Events` - For `IEvent`, `IEventHandler<T>`, and `IEventDispatcher` interfaces

## Best Practices

1. **Use appropriate dispatchers** - Register `ServiceBusCommandDispatcher` and `ServiceBusEventDispatcher` when you want CQRS operations to go through message brokers
2. **Handle correlation context** - The dispatchers automatically propagate correlation context from `ICorrelationContextAccessor`
3. **Register handlers properly** - Ensure command and event handlers are registered in the DI container
4. **Set up subscriptions** - Use `SubscribeCommand<T>()` and `SubscribeEvent<T>()` for clean subscription setup
5. **Choose your pattern** - You can mix in-process CQRS handlers with message broker dispatchers as needed
6. **Monitor message flow** - Since commands/events go through message brokers, use appropriate monitoring and error handling

## Troubleshooting

### Common Issues

1. **Handlers not found**
   - Ensure command/event handlers are registered in DI container
   - Verify handler implementations match the expected interface
   - Check that `AddCommandHandlers()` and `AddEventHandlers()` are called

2. **Messages not being processed**
   - Verify message broker configuration is correct
   - Check that subscriptions are set up using `SubscribeCommand<T>()` and `SubscribeEvent<T>()`
   - Ensure the message broker service is running

3. **Correlation context issues**
   - Verify `ICorrelationContextAccessor` is registered (usually done by message broker packages)
   - Check that correlation context is properly set in incoming requests

4. **Serialization problems**
   - Ensure command/event classes are serializable
   - Check message broker serialization settings
   - Verify data contracts are consistent across services
