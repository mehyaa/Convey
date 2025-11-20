# Convey

The core Convey library provides the foundation for building microservices with .NET. It includes dependency injection extensions, configuration utilities, and application bootstrapping features.

## Installation

```bash
dotnet add package Convey
```

## Overview

Convey is the foundational package that provides:
- **Service registration and configuration** - Fluent API for configuring services
- **Application bootstrapping** - Startup initialization patterns
- **Configuration binding** - Simplified configuration management
- **Service identification** - Unique service instance identification
- **Decorator pattern support** - Service decoration capabilities

## Configuration

Configure Convey in your `Program.cs`:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddInitializer<DatabaseInitializer>()
    .AddInitializer<CacheInitializer>();

var app = builder.Build();
app.Run(); // Initializers run automatically via hosted services
```

### App Options

Configure application metadata in `appsettings.json`:

```json
{
  "app": {
    "name": "MyService",
    "service": "my-service",
    "instance": "instance-001",
    "version": "1.0.0",
    "displayBanner": true,
    "displayVersion": true
  }
}
```

## Key Features

### 1. Service Builder Pattern

The `IConveyBuilder` interface provides a fluent API for configuring services:

```csharp
public interface IConveyBuilder
{
    IServiceCollection Services { get; }
    IConfiguration Configuration { get; }
    bool TryRegister(string name);
    void AddBuildAction(Action<IServiceProvider> action);
    IServiceProvider Build();
}
```

### 2. Configuration Binding

Simplified configuration binding with the `GetOptions<T>()` extension:

```csharp
// Bind configuration section to a POCO
var dbOptions = builder.GetOptions<DatabaseOptions>("database");

// In appsettings.json
{
  "database": {
    "connectionString": "...",
    "timeout": 30
  }
}
```

### 3. Application Initializers

Implement `IInitializer` for startup logic:

```csharp
public class DatabaseInitializer : IInitializer
{
    public async Task InitializeAsync()
    {
        // Database setup logic
        await EnsureDatabaseExists();
        await RunMigrations();
    }
}

// Register the initializer
builder.Services.AddConvey()
    .AddInitializer<DatabaseInitializer>();
```

### 4. Service Identification

Automatic service instance identification:

```csharp
public class MyService
{
    private readonly IServiceId _serviceId;

    public MyService(IServiceId serviceId)
    {
        _serviceId = serviceId;
    }

    public void DoWork()
    {
        var instanceId = _serviceId.Id; // Unique instance identifier
    }
}
```

### 5. Decorator Pattern Support

Mark services for decoration:

```csharp
[Decorator]
public class CachedUserService : IUserService
{
    private readonly IUserService _userService;
    private readonly IMemoryCache _cache;

    public CachedUserService(IUserService userService, IMemoryCache cache)
    {
        _userService = userService;
        _cache = cache;
    }

    public async Task<User> GetUserAsync(int id)
    {
        var cacheKey = $"user:{id}";
        if (_cache.TryGetValue(cacheKey, out User cachedUser))
        {
            return cachedUser;
        }

        var user = await _userService.GetUserAsync(id);
        _cache.Set(cacheKey, user, TimeSpan.FromMinutes(5));
        return user;
    }
}
```

## API Reference

### Extensions

#### AddConvey()
```csharp
public static IConveyBuilder AddConvey(
    this IServiceCollection services,
    string sectionName = "app",
    IConfiguration configuration = null)
```

Registers Convey core services and initializes the builder.

**Parameters:**
- `sectionName` - Configuration section name for app options (default: "app")
- `configuration` - Optional configuration instance

**Returns:** `IConveyBuilder` for fluent configuration

#### AddInitializer&lt;T&gt;()
```csharp
public static IConveyBuilder AddInitializer<TInitializer>(this IConveyBuilder builder)
    where TInitializer : class, IInitializer
```

Registers an application initializer.

**Type Parameters:**
- `TInitializer` - Type implementing `IInitializer`

#### GetOptions&lt;T&gt;()
```csharp
public static TModel GetOptions<TModel>(this IConfiguration configuration, string sectionName)
    where TModel : new()

public static TModel GetOptions<TModel>(this IConveyBuilder builder, string settingsSectionName)
    where TModel : new()
```

Binds a configuration section to a strongly-typed model.

### Interfaces

#### IInitializer
```csharp
public interface IInitializer
{
    Task InitializeAsync();
}
```

Contract for application startup initializers.

#### IServiceId
```csharp
public interface IServiceId
{
    string Id { get; }
}
```

Provides unique service instance identification.

#### IIdentifiable
```csharp
public interface IIdentifiable<out T>
{
    T Id { get; }
}
```

Generic interface for identifiable entities.

## Usage Examples

### Basic Setup
```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey("myapp")
    .AddInitializer<DatabaseInitializer>();

var app = builder.Build();
app.Run();
```

### Custom Configuration Section
```csharp
builder.Services.AddConvey("customSection")
    .AddInitializer<CustomInitializer>();
```

### Multiple Initializers
```csharp
builder.Services.AddConvey()
    .AddInitializer<DatabaseInitializer>()
    .AddInitializer<CacheInitializer>()
    .AddInitializer<MessageBrokerInitializer>();
```

### Configuration Binding
```csharp
public class DatabaseOptions
{
    public string ConnectionString { get; set; }
    public int CommandTimeout { get; set; } = 30;
    public bool EnableRetryOnFailure { get; set; } = true;
}

// In startup
var dbOptions = builder.GetOptions<DatabaseOptions>("database");
builder.Services.AddSingleton(dbOptions);
```

## Best Practices

1. **Use initializers for setup logic** - Keep startup code organized and testable
2. **Bind configuration to POCOs** - Use strongly-typed configuration objects
3. **Leverage service identification** - Use `IServiceId` for instance tracking
4. **Register services through builder** - Use the fluent API for service registration
5. **Keep app options simple** - Focus on essential application metadata

## Integration

Convey serves as the foundation for all other Convey packages. Other packages extend the `IConveyBuilder` with additional functionality:

```csharp
builder.Services.AddConvey()
    .AddWebApi()           // From Convey.WebApi
    .AddCommandHandlers()  // From Convey.CQRS.Commands
    .AddEventHandlers()    // From Convey.CQRS.Events
    .AddRabbitMq()         // From Convey.MessageBrokers.RabbitMQ
    .AddMongo()            // From Convey.Persistence.MongoDB
    .AddJwt();             // From Convey.Auth
```

## Troubleshooting

### Common Issues

1. **Banner not displaying**
   - Ensure `displayBanner: true` in configuration
   - Verify the `name` property is set in app options

2. **Initializers not running**
   - Check that initializers implement `IInitializer`
   - Ensure initializers are registered with `AddInitializer<T>()`

3. **Configuration not binding**
   - Verify section names match between code and configuration
   - Ensure configuration file is properly loaded

4. **Service registration issues**
   - Use `TryRegister()` to avoid duplicate registrations
   - Check service lifetimes and dependencies
