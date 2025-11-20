# Getting Started with Convey

This guide shows a practical end‑to‑end setup of a microservice using Convey’s modular packages. You can copy snippets directly or adapt them to existing solutions.

## Prerequisites

- .NET 8.0 or later
- Basic knowledge of C# and ASP.NET Core
- (Optional) Docker for running dependencies like MongoDB, RabbitMQ

## Creating Your First Microservice

### Step 1: Create a New Project

```bash
dotnet new webapi -n UserService
cd UserService
```

### Step 2: Add Convey Packages

```bash
dotnet add package Convey
dotnet add package Convey.WebApi
dotnet add package Convey.CQRS.Commands
dotnet add package Convey.CQRS.Queries
dotnet add package Convey.Persistence.MongoDB
dotnet add package Convey.MessageBrokers.RabbitMQ
dotnet add package Convey.Auth
```

### Step 3: Configure Services

Update `Program.cs` (minimal hosting, .NET 8):

```csharp
using Convey;
using Convey.Auth;
using Convey.CQRS.Commands;
using Convey.CQRS.Queries;
using Convey.MessageBrokers.RabbitMQ;
using Convey.MessageBrokers; // For IBusPublisher / events
using System.Linq.Expressions; // For repository expressions later
using Convey.Persistence.MongoDB;
using Convey.WebApi;

var builder = WebApplication.CreateBuilder(args);

// Add Convey with required services
builder.Services.AddConvey()
    .AddWebApi()
    .AddJwt()
    .AddCommandHandlers()
    .AddQueryHandlers()
    .AddInMemoryCommandDispatcher() // or rely on RabbitMQ for inter-service messaging
    .AddInMemoryQueryDispatcher()
    .AddMongo()
    .AddRabbitMq(); // Enables message publishing & subscribing

// Register application services
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();

var app = builder.Build();

// Configure the HTTP request pipeline & endpoints
app.UseAuthentication();
app.UseAuthorization();

app.UseEndpoints(endpoints =>
{
    // Public endpoints
    endpoints.Get("/", async ctx => await ctx.Response.WriteAsync("User Service"));
    endpoints.Get("/health", async ctx => await ctx.Response.WriteAsync("Healthy"));

    // User endpoints with CQRS
    endpoints.Get<GetUserQuery>("/api/users/{id:guid}", async (query, ctx) =>
    {
        var queryDispatcher = ctx.RequestServices.GetRequiredService<IQueryDispatcher>();
        var user = await queryDispatcher.QueryAsync(query);

        if (user == null)
        {
            ctx.Response.StatusCode = 404;
            return;
        }

        await ctx.Response.WriteAsJsonAsync(user);
    });

    endpoints.Get<BrowseUsersQuery>("/api/users", async (query, ctx) =>
    {
        var queryDispatcher = ctx.RequestServices.GetRequiredService<IQueryDispatcher>();
        var result = await queryDispatcher.QueryAsync(query);
        await ctx.Response.WriteAsJsonAsync(result);
    });

    endpoints.Post<CreateUserCommand>("/api/users",
        context: async (command, ctx) =>
        {
            var commandDispatcher = ctx.RequestServices.GetRequiredService<ICommandDispatcher>();
            await commandDispatcher.SendAsync(command);

            ctx.Response.StatusCode = 201;
            ctx.Response.Headers["Location"] = $"/api/users/{command.Id}";
        });

    endpoints.Put<UpdateUserCommand>("/api/users/{id:guid}",
        auth: true,
        context: async (command, ctx) =>
        {
            var commandDispatcher = ctx.RequestServices.GetRequiredService<ICommandDispatcher>();
            await commandDispatcher.SendAsync(command);
            ctx.Response.StatusCode = 204;
        });

    endpoints.Delete<DeleteUserCommand>("/api/users/{id:guid}",
        auth: true,
        context: async (command, ctx) =>
        {
            var commandDispatcher = ctx.RequestServices.GetRequiredService<ICommandDispatcher>();
            await commandDispatcher.SendAsync(command);
            ctx.Response.StatusCode = 204;
        });
});

app.UseRabbitMq();
app.Run();
```

### Step 4: Define Your Domain Model

Create `Models/User.cs`:

```csharp
using Convey.Types;

namespace UserService.Models;

public class User : IIdentifiable<Guid>
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public string Role { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsActive { get; set; } = true;

    public User()
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTime.UtcNow;
    }

    public User(string name, string email, string role = "User") : this()
    {
        Name = name;
        Email = email;
        Role = role;
    }
}
```

### Step 5: Create Commands and Queries

Create `Commands/CreateUserCommand.cs`:

```csharp
using Convey.CQRS.Commands;

namespace UserService.Commands;

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

Create `Queries/GetUserQuery.cs`:

```csharp
using Convey.CQRS.Queries;
using UserService.DTO;

namespace UserService.Queries;

public class GetUserQuery : IQuery<UserDto>
{
    public Guid Id { get; set; }
}

public class BrowseUsersQuery : PagedQueryBase, IQuery<PagedResult<UserDto>>
{
    public string Role { get; set; }
    public string Search { get; set; }
}
```

### Step 6: Create DTOs

Create `DTO/UserDto.cs`:

```csharp
namespace UserService.DTO;

public class UserDto
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public string Role { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public static UserDto FromUser(Models.User user)
    {
        return new UserDto
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            Role = user.Role,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }
}
```

### Step 7: Implement Repository

Create `Repositories/IUserRepository.cs`:

```csharp
using Convey.Persistence.MongoDB;
using UserService.Models;

namespace UserService.Repositories;

public interface IUserRepository : IMongoRepository<User, Guid>
{
    Task<User> GetByEmailAsync(string email);
    Task<IReadOnlyList<User>> GetActiveUsersAsync();
}

public class UserRepository : IUserRepository
{
    private readonly IMongoRepository<User, Guid> _repository;

    public UserRepository(IMongoRepository<User, Guid> repository)
    {
        _repository = repository;
    }

    public IMongoCollection<User> Collection => _repository.Collection;

    public Task<User> GetAsync(Guid id) => _repository.GetAsync(id);
    public Task<User> GetAsync(Expression<Func<User, bool>> predicate) => _repository.GetAsync(predicate);
    public Task<IReadOnlyList<User>> FindAsync(Expression<Func<User, bool>> predicate) => _repository.FindAsync(predicate);
    public Task<PagedResult<User>> BrowseAsync<TQuery>(Expression<Func<User, bool>> predicate, TQuery query) where TQuery : IPagedQuery => _repository.BrowseAsync(predicate, query);
    public Task AddAsync(User entity) => _repository.AddAsync(entity);
    public Task UpdateAsync(User entity) => _repository.UpdateAsync(entity);
    public Task UpdateAsync(User entity, Expression<Func<User, bool>> predicate) => _repository.UpdateAsync(entity, predicate);
    public Task DeleteAsync(Guid id) => _repository.DeleteAsync(id);
    public Task DeleteAsync(Expression<Func<User, bool>> predicate) => _repository.DeleteAsync(predicate);
    public Task<bool> ExistsAsync(Expression<Func<User, bool>> predicate) => _repository.ExistsAsync(predicate);

    public Task<User> GetByEmailAsync(string email) => _repository.GetAsync(x => x.Email == email);
    public Task<IReadOnlyList<User>> GetActiveUsersAsync() => _repository.FindAsync(x => x.IsActive);
}
```

### Step 8: Implement Handlers

Create `Handlers/CreateUserHandler.cs`:

```csharp
using Convey.CQRS.Commands;
using UserService.Commands;
using UserService.Events;
using UserService.Models;
using UserService.Repositories;

namespace UserService.Handlers;

public class CreateUserHandler : ICommandHandler<CreateUserCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IBusPublisher _publisher;
    private readonly ILogger<CreateUserHandler> _logger;

    public CreateUserHandler(
        IUserRepository userRepository,
        IBusPublisher publisher,
        ILogger<CreateUserHandler> logger)
    {
        _userRepository = userRepository;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task HandleAsync(CreateUserCommand command, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating user {UserId} with email {Email}", command.Id, command.Email);

        var existingUser = await _userRepository.GetByEmailAsync(command.Email);
        if (existingUser != null)
        {
            throw new InvalidOperationException($"User with email {command.Email} already exists");
        }

        var user = new User(command.Name, command.Email, command.Role)
        {
            Id = command.Id
        };

        await _userRepository.AddAsync(user);

        // Publish integration event
        var @event = new UserCreatedEvent(user.Id, user.Name, user.Email, user.Role);
        await _publisher.PublishAsync(@event);

        _logger.LogInformation("User {UserId} created successfully", command.Id);
    }
}
```

Create `Handlers/GetUserHandler.cs`:

```csharp
using Convey.CQRS.Queries;
using UserService.DTO;
using UserService.Queries;
using UserService.Repositories;

namespace UserService.Handlers;

public class GetUserHandler : IQueryHandler<GetUserQuery, UserDto>
{
    private readonly IUserRepository _userRepository;

    public GetUserHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<UserDto> HandleAsync(GetUserQuery query, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetAsync(query.Id);
        return user != null ? UserDto.FromUser(user) : null;
    }
}

public class BrowseUsersHandler : IQueryHandler<BrowseUsersQuery, PagedResult<UserDto>>
{
    private readonly IUserRepository _userRepository;

    public BrowseUsersHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<PagedResult<UserDto>> HandleAsync(BrowseUsersQuery query, CancellationToken cancellationToken = default)
    {
        Expression<Func<User, bool>> predicate = x => x.IsActive;

        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            predicate = predicate.And(x => x.Role == query.Role);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            predicate = predicate.And(x => x.Name.Contains(query.Search) || x.Email.Contains(query.Search));
        }

        var result = await _userRepository.BrowseAsync(predicate, query);

        return new PagedResult<UserDto>
        {
            Items = result.Items.Select(UserDto.FromUser).ToList(),
            Page = result.Page,
            PageSize = result.PageSize,
            TotalItems = result.TotalItems,
            TotalPages = result.TotalPages
        };
    }
}
```

### Step 9: Define Events

Create `Events/UserCreatedEvent.cs`:

```csharp
using Convey.CQRS.Events;

namespace UserService.Events;

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
```

### Step 10: Configuration

Create `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "app": {
    "name": "User Service",
    "service": "user-service",
    "version": "1.0.0"
  },
  "mongo": {
    "connectionString": "mongodb://localhost:27017",
    "database": "userservice",
    "seed": false
  },
  "rabbitmq": {
    "connectionName": "user-service",
    "hostNames": ["localhost"],
    "port": 5672,
    "virtualHost": "/",
    "username": "guest",
    "password": "guest",
    "retries": 3,
    "retryInterval": 2000,
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
    }
  },
  "jwt": {
    "issuer": "user-service",
    "issuerSigningKey": "your-super-secret-key-that-is-at-least-256-bits-long",
    "audience": "user-service-api",
    "expiry": "01:00:00",
    "validateIssuer": true,
    "validateAudience": true,
    "validateLifetime": true
  }
}
```

### Step 11: Run Your Service

```bash
# Start dependencies (optional, if using Docker)
docker run -d --name mongodb -p 27017:27017 mongo:latest
docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3-management

# Run the service
dotnet run
```

### Step 12: Test Your API

```bash
# Create a user
curl -X POST "http://localhost:5000/api/users" \
     -H "Content-Type: application/json" \
     -d '{
       "id": "123e4567-e89b-12d3-a456-426614174000",
       "name": "John Doe",
       "email": "john@example.com",
       "role": "User"
     }'

# Get a user
curl "http://localhost:5000/api/users/123e4567-e89b-12d3-a456-426614174000"

# Browse users
curl "http://localhost:5000/api/users?page=1&pageSize=10"

# Health check
curl "http://localhost:5000/health"
```

## Next Steps

Now that you have a basic microservice running, you can:

1. **Add Authentication**: Secure your endpoints with JWT tokens
2. **Add Validation**: Implement request validation with FluentValidation
3. **Add Logging**: Integrate structured logging with Serilog
4. **Add Metrics**: Monitor your service with Prometheus metrics
5. **Add Tracing**: Implement distributed tracing with Jaeger
6. **Add Swagger**: Document your API with OpenAPI/Swagger
7. **Add More Services**: Create additional microservices that communicate via events

## Common Patterns

### 1. Adding Authentication

```csharp
// In your endpoints
endpoints.Get<GetUserQuery>("/api/users/{id:guid}",
    auth: true,  // Require authentication
    context: async (query, ctx) => { /* handler */ });

endpoints.Post<CreateUserCommand>("/api/users",
    auth: true,
    roles: "admin",  // Require admin role
    context: async (command, ctx) => { /* handler */ });
```

### 2. Adding Validation

```bash
dotnet add package FluentValidation
dotnet add package FluentValidation.DependencyInjectionExtensions
```

```csharp
public class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Role).NotEmpty().Must(r => new[] { "User", "Admin" }.Contains(r));
    }
}
```

### 3. Adding Event Handlers

```csharp
public class UserCreatedEventHandler : IEventHandler<UserCreatedEvent>
{
    private readonly IEmailService _emailService;

    public UserCreatedEventHandler(IEmailService emailService)
    {
        _emailService = emailService;
    }

    public async Task HandleAsync(UserCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        await _emailService.SendWelcomeEmailAsync(@event.Email, @event.Name);
    }
}
```

## Troubleshooting

### Common Issues

1. **MongoDB Connection Issues**
   - Ensure MongoDB is running
   - Check connection string in appsettings.json
   - Verify network connectivity

2. **RabbitMQ Connection Issues**
   - Ensure RabbitMQ is running
   - Check credentials and virtual host
   - Verify management interface at http://localhost:15672

3. **Handler Not Found**
   - Ensure handlers implement the correct interfaces
   - Check that `AddCommandHandlers()` and `AddQueryHandlers()` are called
   - Verify handler registration in DI container

4. **Route Not Found**
   - Check route patterns in endpoint definitions
   - Verify parameter constraints (e.g., `{id:guid}`)
   - Ensure endpoint methods are called in the correct order

---
This guide provides a solid foundation. For style & conventions see [`STYLEGUIDE.md`](./STYLEGUIDE.md). Consider adding metrics, tracing and structured logging early for production readiness.
