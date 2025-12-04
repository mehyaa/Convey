---
layout: default
title: Convey.Persistence.MongoDB
parent: Persistence
---
# Convey.Persistence.MongoDB

MongoDB integration package providing repository patterns, database initialization, seeding capabilities, and advanced MongoDB features for .NET applications.

## Installation

```bash
dotnet add package Convey.Persistence.MongoDB
```

## Overview

Convey.Persistence.MongoDB provides:
- **Repository pattern** - Generic repository with CRUD operations
- **Database initialization** - Automatic database and collection setup
- **Data seeding** - Initial data population capabilities
- **Pagination support** - Built-in paged query results
- **Session management** - MongoDB session factory for transactions
- **Convention registration** - Automatic BSON serialization conventions
- **Integration testing support** - Random database suffix for tests

## Configuration

### Basic Setup

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddMongo();

var app = builder.Build();
app.Run();
```

### MongoDB Options

Configure in `appsettings.json`:

```json
{
  "mongo": {
    "connectionString": "mongodb://localhost:27017",
    "database": "myapp",
    "seed": false,
    "setRandomDatabaseSuffix": false
  }
}
```

### Advanced Configuration

```csharp
builder.Services.AddConvey()
    .AddMongo(x => x
        .WithConnectionString("mongodb://localhost:27017")
        .WithDatabase("myapp")
        .WithSeed(true)
        .WithRandomDatabaseSuffix(true) // For testing
    );
```

## Key Features

### 1. Entity Models

Define entities that implement `IIdentifiable<T>`:

```csharp
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

public class Product : IIdentifiable<string>
{
    public string Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public string Category { get; set; }
    public string Description { get; set; }
    public int Stock { get; set; }
    public DateTime CreatedAt { get; set; }

    public Product()
    {
        Id = ObjectId.GenerateNewId().ToString();
        CreatedAt = DateTime.UtcNow;
    }
}
```

### 2. Repository Pattern

Use the generic repository for data access:

```csharp
public interface IUserRepository : IMongoRepository<User, Guid>
{
    Task<User> GetByEmailAsync(string email);
    Task<IReadOnlyList<User>> GetActiveUsersAsync();
    Task<PagedResult<User>> BrowseByRoleAsync(string role, IPagedQuery query);
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

    public Task<User> GetAsync(Expression<Func<User, bool>> predicate) =>
        _repository.GetAsync(predicate);

    public Task<IReadOnlyList<User>> FindAsync(Expression<Func<User, bool>> predicate) =>
        _repository.FindAsync(predicate);

    public Task<PagedResult<User>> BrowseAsync<TQuery>(Expression<Func<User, bool>> predicate, TQuery query)
        where TQuery : IPagedQuery => _repository.BrowseAsync(predicate, query);

    public Task AddAsync(User entity) => _repository.AddAsync(entity);

    public Task UpdateAsync(User entity) => _repository.UpdateAsync(entity);

    public Task UpdateAsync(User entity, Expression<Func<User, bool>> predicate) =>
        _repository.UpdateAsync(entity, predicate);

    public Task DeleteAsync(Guid id) => _repository.DeleteAsync(id);

    public Task DeleteAsync(Expression<Func<User, bool>> predicate) =>
        _repository.DeleteAsync(predicate);

    public Task<bool> ExistsAsync(Expression<Func<User, bool>> predicate) =>
        _repository.ExistsAsync(predicate);

    // Custom methods
    public Task<User> GetByEmailAsync(string email) =>
        _repository.GetAsync(x => x.Email == email);

    public Task<IReadOnlyList<User>> GetActiveUsersAsync() =>
        _repository.FindAsync(x => x.IsActive);

    public Task<PagedResult<User>> BrowseByRoleAsync(string role, IPagedQuery query) =>
        _repository.BrowseAsync(x => x.Role == role, query);
}

// Register the repository
builder.Services.AddScoped<IUserRepository, UserRepository>();
```

### 3. Service Layer Implementation

```csharp
public class UserService
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<UserService> _logger;

    public UserService(IUserRepository userRepository, ILogger<UserService> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<User> GetByIdAsync(Guid id)
    {
        return await _userRepository.GetAsync(id);
    }

    public async Task<User> GetByEmailAsync(string email)
    {
        return await _userRepository.GetByEmailAsync(email);
    }

    public async Task<PagedResult<User>> BrowseAsync(BrowseUsersQuery query)
    {
        Expression<Func<User, bool>> predicate = x => x.IsActive;

        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            predicate = predicate.And(x => x.Role == query.Role);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            predicate = predicate.And(x =>
                x.Name.Contains(query.Search) ||
                x.Email.Contains(query.Search));
        }

        return await _userRepository.BrowseAsync(predicate, query);
    }

    public async Task<User> CreateAsync(CreateUserCommand command)
    {
        var existingUser = await _userRepository.GetByEmailAsync(command.Email);
        if (existingUser != null)
        {
            throw new UserAlreadyExistsException(command.Email);
        }

        var user = new User(command.Name, command.Email, command.Role);
        await _userRepository.AddAsync(user);

        _logger.LogInformation("User {UserId} created with email {Email}", user.Id, user.Email);
        return user;
    }

    public async Task UpdateAsync(Guid id, UpdateUserCommand command)
    {
        var user = await _userRepository.GetAsync(id);
        if (user == null)
        {
            throw new UserNotFoundException(id);
        }

        user.Name = command.Name;
        user.Email = command.Email;
        user.UpdatedAt = DateTime.UtcNow;

        await _userRepository.UpdateAsync(user);
        _logger.LogInformation("User {UserId} updated", id);
    }

    public async Task DeleteAsync(Guid id)
    {
        var user = await _userRepository.GetAsync(id);
        if (user == null)
        {
            throw new UserNotFoundException(id);
        }

        await _userRepository.DeleteAsync(id);
        _logger.LogInformation("User {UserId} deleted", id);
    }
}
```

### 4. Data Seeding

Implement automatic data seeding:

```csharp
public class UserSeeder : IMongoDbSeeder
{
    private readonly IUserRepository _userRepository;

    public UserSeeder(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task SeedAsync()
    {
        if (await _userRepository.ExistsAsync(x => true))
        {
            return; // Already seeded
        }

        var users = new[]
        {
            new User("Admin User", "admin@example.com", "Admin"),
            new User("John Doe", "john@example.com", "User"),
            new User("Jane Smith", "jane@example.com", "User"),
            new User("Manager User", "manager@example.com", "Manager")
        };

        foreach (var user in users)
        {
            await _userRepository.AddAsync(user);
        }
    }
}

// Register seeder
builder.Services.AddConvey()
    .AddMongo(seederType: typeof(UserSeeder));
```

### 5. Pagination Queries

Implement paginated queries:

```csharp
public class BrowseUsersQuery : PagedQueryBase
{
    public string Role { get; set; }
    public string Search { get; set; }
    public bool? IsActive { get; set; }
}

public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<UserDto>>> BrowseAsync([FromQuery] BrowseUsersQuery query)
    {
        var result = await _userService.BrowseAsync(query);

        return Ok(new PagedResult<UserDto>
        {
            Items = result.Items.Select(UserDto.FromUser).ToList(),
            Page = result.Page,
            PageSize = result.PageSize,
            TotalItems = result.TotalItems,
            TotalPages = result.TotalPages
        });
    }
}
```

## Advanced Features

### 1. Transactions with Sessions

```csharp
public class OrderService
{
    private readonly IMongoSessionFactory _sessionFactory;
    private readonly IOrderRepository _orderRepository;
    private readonly IProductRepository _productRepository;

    public OrderService(
        IMongoSessionFactory sessionFactory,
        IOrderRepository orderRepository,
        IProductRepository productRepository)
    {
        _sessionFactory = sessionFactory;
        _orderRepository = orderRepository;
        _productRepository = productRepository;
    }

    public async Task<Order> CreateOrderAsync(CreateOrderCommand command)
    {
        using var session = await _sessionFactory.CreateAsync();
        session.StartTransaction();

        try
        {
            // Check product availability
            var product = await _productRepository.GetAsync(command.ProductId);
            if (product.Stock < command.Quantity)
            {
                throw new InsufficientStockException(command.ProductId);
            }

            // Update product stock
            product.Stock -= command.Quantity;
            await _productRepository.UpdateAsync(product);

            // Create order
            var order = new Order(command.CustomerId, command.ProductId, command.Quantity, product.Price);
            await _orderRepository.AddAsync(order);

            await session.CommitTransactionAsync();
            return order;
        }
        catch
        {
            await session.AbortTransactionAsync();
            throw;
        }
    }
}
```

### 2. Custom Repository Implementation

```csharp
public class CustomUserRepository : MongoRepository<User, Guid>, IUserRepository
{
    public CustomUserRepository(IMongoDatabase database) : base(database, "users")
    {
    }

    public async Task<User> GetByEmailAsync(string email)
    {
        return await Collection
            .Find(x => x.Email == email && x.IsActive)
            .FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyList<User>> GetUsersByRoleAsync(string role)
    {
        return await Collection
            .Find(x => x.Role == role && x.IsActive)
            .SortBy(x => x.Name)
            .ToListAsync();
    }

    public async Task<long> GetUserCountByRoleAsync(string role)
    {
        return await Collection.CountDocumentsAsync(x => x.Role == role && x.IsActive);
    }

    public async Task BulkUpdateUserRoleAsync(IEnumerable<Guid> userIds, string newRole)
    {
        var filter = Builders<User>.Filter.In(x => x.Id, userIds);
        var update = Builders<User>.Update
            .Set(x => x.Role, newRole)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        await Collection.UpdateManyAsync(filter, update);
    }
}
```

### 3. Database Initialization

```csharp
public class DatabaseInitializer : IMongoDbInitializer
{
    private readonly IMongoDatabase _database;

    public DatabaseInitializer(IMongoDatabase database)
    {
        _database = database;
    }

    public async Task InitializeAsync()
    {
        // Create indexes
        var userCollection = _database.GetCollection<User>("users");
        await userCollection.Indexes.CreateOneAsync(
            new CreateIndexModel<User>(
                Builders<User>.IndexKeys.Ascending(x => x.Email),
                new CreateIndexOptions { Unique = true }
            )
        );

        await userCollection.Indexes.CreateOneAsync(
            new CreateIndexModel<User>(
                Builders<User>.IndexKeys.Ascending(x => x.Role)
            )
        );

        // Create other collections and indexes
        var productCollection = _database.GetCollection<Product>("products");
        await productCollection.Indexes.CreateOneAsync(
            new CreateIndexModel<Product>(
                Builders<Product>.IndexKeys.Text(x => x.Name).Text(x => x.Description)
            )
        );
    }
}

// Register initializer
builder.Services.AddSingleton<IMongoDbInitializer, DatabaseInitializer>();
```

## API Reference

### IMongoRepository&lt;TEntity, TIdentifiable&gt;

```csharp
public interface IMongoRepository<TEntity, in TIdentifiable> where TEntity : IIdentifiable<TIdentifiable>
{
    IMongoCollection<TEntity> Collection { get; }
    Task<TEntity> GetAsync(TIdentifiable id);
    Task<TEntity> GetAsync(Expression<Func<TEntity, bool>> predicate);
    Task<IReadOnlyList<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate);
    Task<PagedResult<TEntity>> BrowseAsync<TQuery>(Expression<Func<TEntity, bool>> predicate, TQuery query) where TQuery : IPagedQuery;
    Task AddAsync(TEntity entity);
    Task UpdateAsync(TEntity entity);
    Task UpdateAsync(TEntity entity, Expression<Func<TEntity, bool>> predicate);
    Task DeleteAsync(TIdentifiable id);
    Task DeleteAsync(Expression<Func<TEntity, bool>> predicate);
    Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate);
}
```

### Extension Methods

#### AddMongo()
```csharp
public static IConveyBuilder AddMongo(
    this IConveyBuilder builder,
    string sectionName = "mongo",
    Type seederType = null,
    bool registerConventions = true)
```

Registers MongoDB services with configuration from appsettings.

#### AddMongo() with Options Builder
```csharp
public static IConveyBuilder AddMongo(
    this IConveyBuilder builder,
    Func<IMongoDbOptionsBuilder, IMongoDbOptionsBuilder> buildOptions,
    Type seederType = null,
    bool registerConventions = true)
```

Registers MongoDB services with fluent configuration.

## Complete Example

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddMongo(seederType: typeof(DataSeeder))
    .AddCommandHandlers()
    .AddQueryHandlers();

// Register repositories and services
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserService, UserService>();

var app = builder.Build();

// Entity
public class User : IIdentifiable<Guid>
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public string Role { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; } = true;

    public User()
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTime.UtcNow;
    }
}

// Repository
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

    // Implement all interface methods...

    public Task<User> GetByEmailAsync(string email) =>
        _repository.GetAsync(x => x.Email == email);

    public Task<IReadOnlyList<User>> GetActiveUsersAsync() =>
        _repository.FindAsync(x => x.IsActive);
}

// Seeder
public class DataSeeder : IMongoDbSeeder
{
    private readonly IUserRepository _userRepository;

    public DataSeeder(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task SeedAsync()
    {
        if (await _userRepository.ExistsAsync(x => true))
            return;

        await _userRepository.AddAsync(new User
        {
            Name = "Admin",
            Email = "admin@example.com",
            Role = "Admin"
        });
    }
}
```

## Best Practices

1. **Use repository pattern** - Encapsulate data access logic
2. **Implement proper indexing** - Create indexes for frequently queried fields
3. **Handle transactions carefully** - Use sessions for multi-document operations
4. **Use projections** - Select only needed fields for better performance
5. **Implement proper error handling** - Handle MongoDB-specific exceptions
6. **Use bulk operations** - For multiple document operations
7. **Monitor performance** - Use MongoDB profiler and monitoring tools
8. **Implement proper logging** - Log database operations for debugging

## Testing

### Integration Testing

```csharp
public class UserRepositoryTests : IClassFixture<MongoTestFixture>
{
    private readonly IUserRepository _repository;

    public UserRepositoryTests(MongoTestFixture fixture)
    {
        _repository = fixture.GetService<IUserRepository>();
    }

    [Fact]
    public async Task AddAsync_ValidUser_StoresInDatabase()
    {
        // Arrange
        var user = new User("Test User", "test@example.com");

        // Act
        await _repository.AddAsync(user);

        // Assert
        var stored = await _repository.GetAsync(user.Id);
        Assert.NotNull(stored);
        Assert.Equal(user.Name, stored.Name);
        Assert.Equal(user.Email, stored.Email);
    }
}

public class MongoTestFixture : IDisposable
{
    private readonly ServiceProvider _serviceProvider;

    public MongoTestFixture()
    {
        var services = new ServiceCollection();

        services.AddConvey()
            .AddMongo(x => x
                .WithConnectionString("mongodb://localhost:27017")
                .WithDatabase("test_db")
                .WithRandomDatabaseSuffix(true)
            );

        services.AddScoped<IUserRepository, UserRepository>();

        _serviceProvider = services.BuildServiceProvider();
    }

    public T GetService<T>() => _serviceProvider.GetRequiredService<T>();

    public void Dispose() => _serviceProvider?.Dispose();
}
```

## Troubleshooting

### Common Issues

1. **Connection string errors**
   - Verify MongoDB server is running
   - Check connection string format
   - Ensure network connectivity

2. **Serialization issues**
   - Check BSON conventions registration
   - Verify entity property types are supported
   - Use proper MongoDB attributes if needed

3. **Index creation failures**
   - Check for unique constraint violations
   - Verify sufficient database permissions
   - Ensure proper index key specifications

4. **Performance issues**
   - Add appropriate indexes
   - Use projections to limit returned data
   - Consider using aggregation pipelines for complex queries

