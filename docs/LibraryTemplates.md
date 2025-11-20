# Convey Library Documentation Templates

This document provides quick reference templates for the remaining Convey libraries. Each library follows similar patterns for configuration and usage.

## Convey.CQRS.Events

Event handling abstractions for CQRS pattern.

### Installation
```bash
dotnet add package Convey.CQRS.Events
```

### Basic Usage
```csharp
// Event definition
public class UserCreatedEvent : IEvent
{
    public Guid UserId { get; set; }
    public string Name { get; set; }
}

// Event handler
public class UserCreatedEventHandler : IEventHandler<UserCreatedEvent>
{
    public async Task HandleAsync(UserCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        // Handle the event
    }
}

// Registration
builder.Services.AddConvey()
    .AddEventHandlers()
    .AddInMemoryEventDispatcher();
```

---

## Convey.CQRS.Queries

Query handling abstractions for CQRS pattern.

### Installation
```bash
dotnet add package Convey.CQRS.Queries
```

### Basic Usage
```csharp
// Query definition
public class GetUserQuery : IQuery<UserDto>
{
    public Guid Id { get; set; }
}

// Query handler
public class GetUserHandler : IQueryHandler<GetUserQuery, UserDto>
{
    public async Task<UserDto> HandleAsync(GetUserQuery query, CancellationToken cancellationToken = default)
    {
        // Handle the query and return result
        return new UserDto();
    }
}

// Registration
builder.Services.AddConvey()
    .AddQueryHandlers()
    .AddInMemoryQueryDispatcher();
```

---

## Convey.Logging

Serilog integration with structured logging.

### Installation
```bash
dotnet add package Convey.Logging
```

### Configuration
```json
{
  "logger": {
    "level": "information",
    "consoleEnabled": true,
    "fileEnabled": true,
    "filePath": "logs/log-.txt",
    "seqEnabled": false,
    "seqUrl": "http://localhost:5341"
  }
}
```

### Basic Usage
```csharp
builder.Services.AddConvey()
    .AddLogging();

// In your classes
public class UserService
{
    private readonly ILogger<UserService> _logger;

    public UserService(ILogger<UserService> logger)
    {
        _logger = logger;
    }

    public async Task CreateUserAsync(User user)
    {
        _logger.LogInformation("Creating user {UserId}", user.Id);
        // ...
    }
}
```

---

## Convey.Metrics.Prometheus

Prometheus metrics integration.

### Installation
```bash
dotnet add package Convey.Metrics.Prometheus
```

### Configuration
```json
{
  "prometheus": {
    "enabled": true,
    "endpoint": "/metrics"
  }
}
```

### Basic Usage
```csharp
builder.Services.AddConvey()
    .AddMetrics();

app.UsePrometheus();
```

---

## Convey.Tracing.Jaeger

Jaeger distributed tracing integration.

### Installation
```bash
dotnet add package Convey.Tracing.Jaeger
```

### Configuration
```json
{
  "jaeger": {
    "enabled": true,
    "serviceName": "user-service",
    "udpHost": "localhost",
    "udpPort": 6831,
    "sampler": "const"
  }
}
```

### Basic Usage
```csharp
builder.Services.AddConvey()
    .AddJaeger();

app.UseJaeger();
```

---

## Convey.Discovery.Consul

Consul service discovery integration.

### Installation
```bash
dotnet add package Convey.Discovery.Consul
```

### Configuration
```json
{
  "consul": {
    "enabled": true,
    "url": "http://localhost:8500",
    "service": "user-service",
    "address": "localhost",
    "port": 5000,
    "pingEnabled": true,
    "pingEndpoint": "health",
    "pingInterval": 3
  }
}
```

### Basic Usage
```csharp
builder.Services.AddConvey()
    .AddConsul();
```

---

## Convey.LoadBalancing.Fabio

Fabio load balancer integration.

### Installation
```bash
dotnet add package Convey.LoadBalancing.Fabio
```

### Configuration
```json
{
  "fabio": {
    "enabled": true,
    "url": "http://localhost:9999",
    "service": "user-service"
  }
}
```

---

## Convey.HTTP.RestEase

RestEase HTTP client integration.

### Installation
```bash
dotnet add package Convey.HTTP.RestEase
```

### Basic Usage
```csharp
[SerializationMethods(Query = QuerySerializationMethod.Serialized)]
public interface IUserService
{
    [Get("users/{id}")]
    Task<UserDto> GetAsync([Path] Guid id);

    [Post("users")]
    Task<UserDto> CreateAsync([Body] CreateUserRequest request);
}

// Registration
builder.Services.AddConvey()
    .AddHttpClient()
    .RegisterServiceForwarder<IUserService>("user-service");
```

---

## Convey.Persistence.Redis

Redis caching and data persistence.

### Installation
```bash
dotnet add package Convey.Persistence.Redis
```

### Configuration
```json
{
  "redis": {
    "connectionString": "localhost:6379",
    "instance": "user-service:",
    "database": 0
  }
}
```

### Basic Usage
```csharp
builder.Services.AddConvey()
    .AddRedis();

// Usage
public class UserService
{
    private readonly IRedisRepository _redis;

    public async Task<User> GetUserAsync(Guid id)
    {
        var cacheKey = $"user:{id}";
        var cached = await _redis.GetAsync<User>(cacheKey);

        if (cached != null)
            return cached;

        var user = await _repository.GetAsync(id);
        await _redis.SetAsync(cacheKey, user, TimeSpan.FromMinutes(5));

        return user;
    }
}
```

---

## Convey.Secrets.Vault

HashiCorp Vault secrets management.

### Installation
```bash
dotnet add package Convey.Secrets.Vault
```

### Configuration
```json
{
  "vault": {
    "enabled": true,
    "url": "http://localhost:8200",
    "authType": "token",
    "token": "your-vault-token",
    "mountPoint": "secret",
    "path": "user-service"
  }
}
```

### Basic Usage
```csharp
builder.Services.AddConvey()
    .AddVault();

// Secrets are automatically loaded into configuration
```

---

## Convey.Security

Security extensions for certificates, mTLS, and encryption.

### Installation
```bash
dotnet add package Convey.Security
```

### Basic Usage
```csharp
builder.Services.AddConvey()
    .AddSecurity();

// Certificate management, encryption utilities, etc.
```

---

## Convey.Docs.Swagger / Convey.WebApi.Swagger

Swagger/OpenAPI documentation.

### Installation
```bash
dotnet add package Convey.Docs.Swagger
# or
dotnet add package Convey.WebApi.Swagger
```

### Basic Usage
```csharp
builder.Services.AddConvey()
    .AddSwaggerDocs();

app.UseSwaggerDocs();
```

---

## Message Broker Extensions

### Convey.MessageBrokers.CQRS
CQRS integration for message brokers.

### Convey.MessageBrokers.Outbox
Outbox pattern implementation.

### Convey.MessageBrokers.Outbox.EntityFramework
EF Core outbox implementation.

### Convey.MessageBrokers.Outbox.Mongo
MongoDB outbox implementation.

All follow similar patterns:

```bash
dotnet add package Convey.MessageBrokers.CQRS
```

```csharp
builder.Services.AddConvey()
    .AddRabbitMq()
    .AddMessageOutbox(); // For outbox patterns
```

---

## Integration Example

Here's how multiple Convey packages work together:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddWebApi()
    .AddJwt()
    .AddCommandHandlers()
    .AddQueryHandlers()
    .AddEventHandlers()
    .AddInMemoryCommandDispatcher()
    .AddInMemoryQueryDispatcher()
    .AddInMemoryEventDispatcher()
    .AddMongo()
    .AddRabbitMq()
    .AddRedis()
    .AddLogging()
    .AddMetrics()
    .AddJaeger()
    .AddConsul()
    .AddSwaggerDocs();

var app = builder.Build();

app.UseSwaggerDocs();
app.UseAuthentication();
app.UseAuthorization();
app.UsePrometheus();
app.UseJaeger();

app.UseEndpoints(endpoints =>
{
    // Define your endpoints
});

app.UseRabbitMq();

app.Run();
```

## Documentation Structure

Each library documentation typically includes:

1. **Installation** - NuGet package installation
2. **Overview** - What the library provides
3. **Configuration** - appsettings.json setup
4. **Key Features** - Main functionality with examples
5. **API Reference** - Interfaces and extension methods
6. **Integration Examples** - How to use with other libraries
7. **Best Practices** - Recommended patterns
8. **Troubleshooting** - Common issues and solutions

For detailed documentation of any specific library, refer to the individual markdown files in this documentation folder.
