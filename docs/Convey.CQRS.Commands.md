---
layout: default
title: Convey.CQRS.Commands
parent: CQRS
---
# Convey.CQRS.Commands

Provides CQRS (Command Query Responsibility Segregation) command handling abstractions and infrastructure. Implements the command pattern for handling write operations in a decoupled, testable manner.

## Installation

```bash
dotnet add package Convey.CQRS.Commands
```

## Overview

Convey.CQRS.Commands provides:
- **Command abstractions** - Interfaces for commands and command handlers
- **Command dispatcher** - In-memory command dispatching
- **Automatic handler registration** - Convention-based handler discovery
- **Decorator support** - Command pipeline decorators
- **Cancellation support** - Built-in cancellation token support

## Configuration

### Basic Setup

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddCommandHandlers()
    .AddInMemoryCommandDispatcher();

var app = builder.Build();
app.Run();
```

## Key Concepts

### 1. Commands

Commands represent write operations and implement the `ICommand` marker interface:

```csharp
public class CreateUserCommand : ICommand
{
    public Guid Id { get; }
    public string Name { get; }
    public string Email { get; }
    public string Role { get; }

    public CreateUserCommand(Guid id, string name, string email, string role = "User")
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        Name = name;
        Email = email;
        Role = role;
    }
}

public class UpdateUserCommand : ICommand
{
    public Guid Id { get; }
    public string Name { get; }
    public string Email { get; }

    public UpdateUserCommand(Guid id, string name, string email)
    {
        Id = id;
        Name = name;
        Email = email;
    }
}

public class DeleteUserCommand : ICommand
{
    public Guid Id { get; }

    public DeleteUserCommand(Guid id)
    {
        Id = id;
    }
}
```

### 2. Command Handlers

Command handlers implement business logic for commands:

```csharp
public class CreateUserHandler : ICommandHandler<CreateUserCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IEmailService _emailService;
    private readonly ILogger<CreateUserHandler> _logger;

    public CreateUserHandler(
        IUserRepository userRepository,
        IEmailService emailService,
        ILogger<CreateUserHandler> logger)
    {
        _userRepository = userRepository;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task HandleAsync(CreateUserCommand command, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating user {UserId} with email {Email}", command.Id, command.Email);

        // Check if user already exists
        var existingUser = await _userRepository.GetByEmailAsync(command.Email);
        if (existingUser != null)
        {
            throw new UserAlreadyExistsException(command.Email);
        }

        // Create user
        var user = new User(command.Id, command.Name, command.Email, command.Role);
        await _userRepository.AddAsync(user);

        // Send welcome email
        await _emailService.SendWelcomeEmailAsync(user.Email, user.Name);

        _logger.LogInformation("User {UserId} created successfully", command.Id);
    }
}

public class UpdateUserHandler : ICommandHandler<UpdateUserCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<UpdateUserHandler> _logger;

    public UpdateUserHandler(IUserRepository userRepository, ILogger<UpdateUserHandler> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task HandleAsync(UpdateUserCommand command, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating user {UserId}", command.Id);

        var user = await _userRepository.GetByIdAsync(command.Id);
        if (user == null)
        {
            throw new UserNotFoundException(command.Id);
        }

        user.UpdateProfile(command.Name, command.Email);
        await _userRepository.UpdateAsync(user);

        _logger.LogInformation("User {UserId} updated successfully", command.Id);
    }
}
```

### 3. Command Dispatcher

The command dispatcher routes commands to their respective handlers:

```csharp
public class UserController : ControllerBase
{
    private readonly ICommandDispatcher _commandDispatcher;

    public UserController(ICommandDispatcher commandDispatcher)
    {
        _commandDispatcher = commandDispatcher;
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        var command = new CreateUserCommand(
            Guid.NewGuid(),
            request.Name,
            request.Email,
            request.Role
        );

        await _commandDispatcher.SendAsync(command);
        return Created($"/api/users/{command.Id}", new { Id = command.Id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest request)
    {
        var command = new UpdateUserCommand(id, request.Name, request.Email);
        await _commandDispatcher.SendAsync(command);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        var command = new DeleteUserCommand(id);
        await _commandDispatcher.SendAsync(command);
        return NoContent();
    }
}
```

## Advanced Features

### 1. Command Decorators

Create command pipeline decorators for cross-cutting concerns:

```csharp
[Decorator]
public class LoggingCommandHandlerDecorator<T> : ICommandHandler<T> where T : class, ICommand
{
    private readonly ICommandHandler<T> _handler;
    private readonly ILogger<LoggingCommandHandlerDecorator<T>> _logger;

    public LoggingCommandHandlerDecorator(
        ICommandHandler<T> handler,
        ILogger<LoggingCommandHandlerDecorator<T>> logger)
    {
        _handler = handler;
        _logger = logger;
    }

    public async Task HandleAsync(T command, CancellationToken cancellationToken = default)
    {
        var commandName = typeof(T).Name;
        _logger.LogInformation("Handling command {CommandName}: {@Command}", commandName, command);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            await _handler.HandleAsync(command, cancellationToken);
            stopwatch.Stop();

            _logger.LogInformation("Command {CommandName} handled successfully in {ElapsedMs}ms",
                commandName, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error handling command {CommandName} after {ElapsedMs}ms",
                commandName, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}

[Decorator]
public class ValidationCommandHandlerDecorator<T> : ICommandHandler<T> where T : class, ICommand
{
    private readonly ICommandHandler<T> _handler;
    private readonly IValidator<T> _validator;

    public ValidationCommandHandlerDecorator(ICommandHandler<T> handler, IValidator<T> validator)
    {
        _handler = handler;
        _validator = validator;
    }

    public async Task HandleAsync(T command, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        await _handler.HandleAsync(command, cancellationToken);
    }
}
```

### 2. Transactional Commands

Implement transactional command handling:

```csharp
[Decorator]
public class TransactionalCommandHandlerDecorator<T> : ICommandHandler<T> where T : class, ICommand
{
    private readonly ICommandHandler<T> _handler;
    private readonly IUnitOfWork _unitOfWork;

    public TransactionalCommandHandlerDecorator(ICommandHandler<T> handler, IUnitOfWork unitOfWork)
    {
        _handler = handler;
        _unitOfWork = unitOfWork;
    }

    public async Task HandleAsync(T command, CancellationToken cancellationToken = default)
    {
        using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _handler.HandleAsync(command, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

// Usage with specific commands
public class CreateOrderCommand : ICommand
{
    public Guid Id { get; }
    public Guid CustomerId { get; }
    public List<OrderItem> Items { get; }

    // ... constructor
}

[Decorator]
public class CreateOrderHandler : ICommandHandler<CreateOrderCommand>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IInventoryService _inventoryService;
    private readonly IPaymentService _paymentService;

    public CreateOrderHandler(
        IOrderRepository orderRepository,
        IInventoryService inventoryService,
        IPaymentService paymentService)
    {
        _orderRepository = orderRepository;
        _inventoryService = inventoryService;
        _paymentService = paymentService;
    }

    public async Task HandleAsync(CreateOrderCommand command, CancellationToken cancellationToken = default)
    {
        // Reserve inventory
        await _inventoryService.ReserveItemsAsync(command.Items);

        // Process payment
        var payment = await _paymentService.ProcessPaymentAsync(command.CustomerId, command.TotalAmount);

        // Create order
        var order = new Order(command.Id, command.CustomerId, command.Items, payment.Id);
        await _orderRepository.AddAsync(order);

        // All operations are wrapped in transaction by decorator
    }
}
```

### 3. Async Command Processing

Handle long-running commands asynchronously:

```csharp
public class ProcessLargeDataImportCommand : ICommand
{
    public Guid ImportId { get; }
    public string FilePath { get; }
    public string UserId { get; }

    public ProcessLargeDataImportCommand(Guid importId, string filePath, string userId)
    {
        ImportId = importId;
        FilePath = filePath;
        UserId = userId;
    }
}

public class ProcessLargeDataImportHandler : ICommandHandler<ProcessLargeDataImportCommand>
{
    private readonly IDataImportService _importService;
    private readonly IBackgroundTaskQueue _taskQueue;

    public ProcessLargeDataImportHandler(
        IDataImportService importService,
        IBackgroundTaskQueue taskQueue)
    {
        _importService = importService;
        _taskQueue = taskQueue;
    }

    public async Task HandleAsync(ProcessLargeDataImportCommand command, CancellationToken cancellationToken = default)
    {
        // Queue the actual processing as a background task
        _taskQueue.QueueBackgroundWorkItem(async token =>
        {
            await _importService.ProcessFileAsync(command.ImportId, command.FilePath, token);
        });

        // Mark import as started
        await _importService.MarkAsStartedAsync(command.ImportId);
    }
}
```

## API Reference

### Interfaces

#### ICommand
```csharp
public interface ICommand
{
    // Marker interface - no members
}
```

Marker interface that all commands must implement.

#### ICommandHandler&lt;T&gt;
```csharp
public interface ICommandHandler<in TCommand> where TCommand : class, ICommand
{
    Task HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}
```

Interface for command handlers.

**Type Parameters:**
- `TCommand` - Type of command to handle

**Methods:**
- `HandleAsync()` - Handles the command asynchronously

#### ICommandDispatcher
```csharp
public interface ICommandDispatcher
{
    Task SendAsync<T>(T command, CancellationToken cancellationToken = default) where T : class, ICommand;
}
```

Interface for dispatching commands to their handlers.

**Methods:**
- `SendAsync<T>()` - Dispatches a command to its handler

### Extension Methods

#### AddCommandHandlers()
```csharp
public static IConveyBuilder AddCommandHandlers(this IConveyBuilder builder)
```

Registers all command handlers found in loaded assemblies.

#### AddInMemoryCommandDispatcher()
```csharp
public static IConveyBuilder AddInMemoryCommandDispatcher(this IConveyBuilder builder)
```

Registers the in-memory command dispatcher implementation.

## Integration Examples

### With Web API

```csharp
// Using Convey.WebApi
app.UseEndpoints(endpoints =>
{
    endpoints.Post<CreateUserCommand>("/api/users", async (command, ctx) =>
    {
        await commandDispatcher.SendAsync(command);
        ctx.Response.StatusCode = 201;
        ctx.Response.Headers["Location"] = $"/api/users/{command.Id}";
    });

    endpoints.Put<UpdateUserCommand>("/api/users/{id:guid}", async (command, ctx) =>
    {
        await commandDispatcher.SendAsync(command);
        ctx.Response.StatusCode = 204;
    });
});

// Using MVC Controllers
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly ICommandDispatcher _dispatcher;

    public UsersController(ICommandDispatcher dispatcher) => _dispatcher = dispatcher;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserCommand command)
    {
        await _dispatcher.SendAsync(command);
        return CreatedAtAction(nameof(GetById), new { id = command.Id }, null);
    }
}
```

### With Message Brokers

```csharp
// Publish commands as events after handling
[Decorator]
public class EventPublishingCommandHandlerDecorator<T> : ICommandHandler<T> where T : class, ICommand
{
    private readonly ICommandHandler<T> _handler;
    private readonly IEventPublisher _eventPublisher;

    public EventPublishingCommandHandlerDecorator(ICommandHandler<T> handler, IEventPublisher eventPublisher)
    {
        _handler = handler;
        _eventPublisher = eventPublisher;
    }

    public async Task HandleAsync(T command, CancellationToken cancellationToken = default)
    {
        await _handler.HandleAsync(command, cancellationToken);

        // Publish domain events based on command type
        var events = MapCommandToEvents(command);
        foreach (var @event in events)
        {
            await _eventPublisher.PublishAsync(@event, cancellationToken);
        }
    }

    private IEnumerable<IEvent> MapCommandToEvents(T command)
    {
        // Map commands to domain events
        return command switch
        {
            CreateUserCommand cmd => new[] { new UserCreatedEvent(cmd.Id, cmd.Name, cmd.Email) },
            UpdateUserCommand cmd => new[] { new UserUpdatedEvent(cmd.Id, cmd.Name, cmd.Email) },
            DeleteUserCommand cmd => new[] { new UserDeletedEvent(cmd.Id) },
            _ => Enumerable.Empty<IEvent>()
        };
    }
}
```

## Best Practices

1. **Keep commands simple** - Commands should be simple data structures
2. **Use immutable commands** - Make command properties read-only
3. **Single responsibility** - One command should do one thing
4. **Validate early** - Use validation decorators
5. **Handle exceptions** - Implement proper error handling
6. **Use meaningful names** - Command names should clearly indicate intent
7. **Avoid business logic in commands** - Keep logic in handlers
8. **Use decorators for cross-cutting concerns** - Logging, validation, transactions

## Testing

### Unit Testing Command Handlers

```csharp
public class CreateUserHandlerTests
{
    private readonly Mock<IUserRepository> _userRepository;
    private readonly Mock<IEmailService> _emailService;
    private readonly CreateUserHandler _handler;

    public CreateUserHandlerTests()
    {
        _userRepository = new Mock<IUserRepository>();
        _emailService = new Mock<IEmailService>();
        _handler = new CreateUserHandler(_userRepository.Object, _emailService.Object, Mock.Of<ILogger<CreateUserHandler>>());
    }

    [Fact]
    public async Task HandleAsync_ValidCommand_CreatesUser()
    {
        // Arrange
        var command = new CreateUserCommand(Guid.NewGuid(), "John Doe", "john@example.com");
        _userRepository.Setup(x => x.GetByEmailAsync(command.Email)).ReturnsAsync((User)null);

        // Act
        await _handler.HandleAsync(command);

        // Assert
        _userRepository.Verify(x => x.AddAsync(It.IsAny<User>()), Times.Once);
        _emailService.Verify(x => x.SendWelcomeEmailAsync(command.Email, command.Name), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_UserExists_ThrowsException()
    {
        // Arrange
        var command = new CreateUserCommand(Guid.NewGuid(), "John Doe", "john@example.com");
        var existingUser = new User(Guid.NewGuid(), "Jane Doe", "john@example.com", "User");
        _userRepository.Setup(x => x.GetByEmailAsync(command.Email)).ReturnsAsync(existingUser);

        // Act & Assert
        await Assert.ThrowsAsync<UserAlreadyExistsException>(() => _handler.HandleAsync(command));
        _userRepository.Verify(x => x.AddAsync(It.IsAny<User>()), Times.Never);
    }
}
```

### Integration Testing

```csharp
public class CommandIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly ICommandDispatcher _dispatcher;

    public CommandIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _dispatcher = _factory.Services.GetRequiredService<ICommandDispatcher>();
    }

    [Fact]
    public async Task CreateUserCommand_Integration_Success()
    {
        // Arrange
        var command = new CreateUserCommand(Guid.NewGuid(), "Test User", "test@example.com");

        // Act
        await _dispatcher.SendAsync(command);

        // Assert
        var userRepository = _factory.Services.GetRequiredService<IUserRepository>();
        var user = await userRepository.GetByIdAsync(command.Id);
        Assert.NotNull(user);
        Assert.Equal(command.Name, user.Name);
        Assert.Equal(command.Email, user.Email);
    }
}
```

## Troubleshooting

### Common Issues

1. **Handler not found**
   - Ensure handlers implement `ICommandHandler<T>`
   - Check that `AddCommandHandlers()` is called
   - Verify handler is not marked with `[Decorator]` attribute

2. **Multiple handlers registered**
   - Only one handler per command type is allowed
   - Check for duplicate handler registrations
   - Use decorators for additional behaviors

3. **Dependency injection errors**
   - Ensure all handler dependencies are registered
   - Check service lifetimes compatibility
   - Verify circular dependencies don't exist

4. **Performance issues**
   - Use async/await properly in handlers
   - Consider background processing for long-running commands
   - Implement proper exception handling to avoid retries

