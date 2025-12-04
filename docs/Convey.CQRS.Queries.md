---
layout: default
title: Convey.CQRS.Queries
parent: CQRS
---
# Convey.CQRS.Queries

Query handling abstractions for CQRS pattern providing query dispatching, handling, and support for read-optimized operations in microservices architecture.

## Installation

```bash
dotnet add package Convey.CQRS.Queries
```

## Overview

Convey.CQRS.Queries provides:
- **Query abstractions** - Interfaces for queries and query handlers
- **Query dispatcher** - In-memory query dispatching
- **Automatic handler registration** - Convention-based handler discovery
- **Decorator support** - Query pipeline decorators
- **Pagination support** - Built-in paged query results
- **Cancellation support** - Built-in cancellation token support

## Configuration

### Basic Setup

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddQueryHandlers()
    .AddInMemoryQueryDispatcher();

var app = builder.Build();
app.Run();
```

## Key Concepts

### 1. Queries

Queries represent read operations and implement the `IQuery<TResult>` interface:

```csharp
public class GetUserQuery : IQuery<UserDto>
{
    public Guid Id { get; set; }

    public GetUserQuery(Guid id)
    {
        Id = id;
    }
}

public class BrowseUsersQuery : PagedQueryBase, IQuery<PagedResult<UserDto>>
{
    public string Role { get; set; }
    public string Search { get; set; }
    public bool? IsActive { get; set; }
    public DateTime? CreatedFrom { get; set; }
    public DateTime? CreatedTo { get; set; }

    public BrowseUsersQuery()
    {
        Page = 1;
        PageSize = 10;
    }
}

public class GetUserStatisticsQuery : IQuery<UserStatisticsDto>
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string[] Roles { get; set; }

    public GetUserStatisticsQuery(DateTime fromDate, DateTime toDate, params string[] roles)
    {
        FromDate = fromDate;
        ToDate = toDate;
        Roles = roles ?? Array.Empty<string>();
    }
}
```

### 2. Query Results (DTOs)

Define read-optimized data transfer objects:

```csharp
public class UserDto
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public string Role { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public bool IsActive { get; set; }

    public static UserDto FromUser(User user)
    {
        return new UserDto
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            Role = user.Role,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt,
            IsActive = user.IsActive
        };
    }
}

public class UserStatisticsDto
{
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int NewUsersThisMonth { get; set; }
    public Dictionary<string, int> UsersByRole { get; set; } = new();
    public DateTime LastCalculated { get; set; }
}

public class OrderSummaryDto
{
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; }
    public string CustomerEmail { get; set; }
    public DateTime OrderDate { get; set; }
    public string Status { get; set; }
    public decimal TotalAmount { get; set; }
    public int ItemCount { get; set; }
}
```

### 3. Query Handlers

Query handlers implement read logic for queries:

```csharp
public class GetUserQueryHandler : IQueryHandler<GetUserQuery, UserDto>
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<GetUserQueryHandler> _logger;

    public GetUserQueryHandler(IUserRepository userRepository, ILogger<GetUserQueryHandler> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<UserDto> HandleAsync(GetUserQuery query, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting user {UserId}", query.Id);

        var user = await _userRepository.GetAsync(query.Id);
        if (user == null)
        {
            _logger.LogWarning("User {UserId} not found", query.Id);
            return null;
        }

        return UserDto.FromUser(user);
    }
}

public class BrowseUsersQueryHandler : IQueryHandler<BrowseUsersQuery, PagedResult<UserDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<BrowseUsersQueryHandler> _logger;

    public BrowseUsersQueryHandler(IUserRepository userRepository, ILogger<BrowseUsersQueryHandler> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<PagedResult<UserDto>> HandleAsync(BrowseUsersQuery query, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Browsing users with filters: Role={Role}, Search={Search}, Page={Page}",
            query.Role, query.Search, query.Page);

        Expression<Func<User, bool>> predicate = x => true;

        // Apply filters
        if (query.IsActive.HasValue)
        {
            predicate = predicate.And(x => x.IsActive == query.IsActive.Value);
        }

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

        if (query.CreatedFrom.HasValue)
        {
            predicate = predicate.And(x => x.CreatedAt >= query.CreatedFrom.Value);
        }

        if (query.CreatedTo.HasValue)
        {
            predicate = predicate.And(x => x.CreatedAt <= query.CreatedTo.Value);
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

public class GetUserStatisticsQueryHandler : IQueryHandler<GetUserStatisticsQuery, UserStatisticsDto>
{
    private readonly IUserRepository _userRepository;
    private readonly ICacheService _cacheService;

    public GetUserStatisticsQueryHandler(IUserRepository userRepository, ICacheService cacheService)
    {
        _userRepository = userRepository;
        _cacheService = cacheService;
    }

    public async Task<UserStatisticsDto> HandleAsync(GetUserStatisticsQuery query, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"user_stats_{query.FromDate:yyyyMM}_{query.ToDate:yyyyMM}_{string.Join("_", query.Roles)}";

        var cached = await _cacheService.GetAsync<UserStatisticsDto>(cacheKey);
        if (cached != null)
        {
            return cached;
        }

        var totalUsers = await _userRepository.CountAsync(x =>
            x.CreatedAt >= query.FromDate && x.CreatedAt <= query.ToDate);

        var activeUsers = await _userRepository.CountAsync(x =>
            x.IsActive && x.CreatedAt >= query.FromDate && x.CreatedAt <= query.ToDate);

        var thisMonth = DateTime.UtcNow.AddMonths(-1);
        var newUsersThisMonth = await _userRepository.CountAsync(x => x.CreatedAt >= thisMonth);

        var usersByRole = new Dictionary<string, int>();
        foreach (var role in query.Roles)
        {
            var count = await _userRepository.CountAsync(x =>
                x.Role == role && x.CreatedAt >= query.FromDate && x.CreatedAt <= query.ToDate);
            usersByRole[role] = count;
        }

        var statistics = new UserStatisticsDto
        {
            TotalUsers = totalUsers,
            ActiveUsers = activeUsers,
            NewUsersThisMonth = newUsersThisMonth,
            UsersByRole = usersByRole,
            LastCalculated = DateTime.UtcNow
        };

        await _cacheService.SetAsync(cacheKey, statistics, TimeSpan.FromMinutes(15));
        return statistics;
    }
}
```

### 4. Query Dispatcher

The query dispatcher routes queries to their respective handlers:

```csharp
public class UserController : ControllerBase
{
    private readonly IQueryDispatcher _queryDispatcher;

    public UserController(IQueryDispatcher queryDispatcher)
    {
        _queryDispatcher = queryDispatcher;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserDto>> GetUser(Guid id)
    {
        var query = new GetUserQuery(id);
        var user = await _queryDispatcher.QueryAsync(query);

        if (user == null)
        {
            return NotFound();
        }

        return Ok(user);
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<UserDto>>> BrowseUsers([FromQuery] BrowseUsersQuery query)
    {
        var result = await _queryDispatcher.QueryAsync(query);
        return Ok(result);
    }

    [HttpGet("statistics")]
    public async Task<ActionResult<UserStatisticsDto>> GetStatistics(
        [FromQuery] DateTime fromDate,
        [FromQuery] DateTime toDate,
        [FromQuery] string[] roles)
    {
        var query = new GetUserStatisticsQuery(fromDate, toDate, roles);
        var statistics = await _queryDispatcher.QueryAsync(query);
        return Ok(statistics);
    }
}
```

## Advanced Features

### 1. Query Decorators

Create query pipeline decorators for cross-cutting concerns:

```csharp
[Decorator]
public class CachingQueryHandlerDecorator<TQuery, TResult> : IQueryHandler<TQuery, TResult>
    where TQuery : class, IQuery<TResult>
{
    private readonly IQueryHandler<TQuery, TResult> _handler;
    private readonly ICacheService _cacheService;
    private readonly ILogger<CachingQueryHandlerDecorator<TQuery, TResult>> _logger;

    public CachingQueryHandlerDecorator(
        IQueryHandler<TQuery, TResult> handler,
        ICacheService cacheService,
        ILogger<CachingQueryHandlerDecorator<TQuery, TResult>> logger)
    {
        _handler = handler;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken = default)
    {
        var cacheKey = GenerateCacheKey(query);

        var cached = await _cacheService.GetAsync<TResult>(cacheKey);
        if (cached != null)
        {
            _logger.LogDebug("Cache hit for query {QueryType} with key {CacheKey}", typeof(TQuery).Name, cacheKey);
            return cached;
        }

        _logger.LogDebug("Cache miss for query {QueryType} with key {CacheKey}", typeof(TQuery).Name, cacheKey);

        var result = await _handler.HandleAsync(query, cancellationToken);

        if (result != null)
        {
            var ttl = GetCacheTtl(query);
            await _cacheService.SetAsync(cacheKey, result, ttl);
        }

        return result;
    }

    private string GenerateCacheKey(TQuery query)
    {
        var queryJson = JsonSerializer.Serialize(query);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(queryJson));
        return $"{typeof(TQuery).Name}_{Convert.ToHexString(hash)[..16]}";
    }

    private TimeSpan GetCacheTtl(TQuery query)
    {
        // Implement cache TTL logic based on query type
        return query switch
        {
            GetUserQuery => TimeSpan.FromMinutes(5),
            BrowseUsersQuery => TimeSpan.FromMinutes(2),
            GetUserStatisticsQuery => TimeSpan.FromMinutes(15),
            _ => TimeSpan.FromMinutes(1)
        };
    }
}

[Decorator]
public class LoggingQueryHandlerDecorator<TQuery, TResult> : IQueryHandler<TQuery, TResult>
    where TQuery : class, IQuery<TResult>
{
    private readonly IQueryHandler<TQuery, TResult> _handler;
    private readonly ILogger<LoggingQueryHandlerDecorator<TQuery, TResult>> _logger;

    public LoggingQueryHandlerDecorator(
        IQueryHandler<TQuery, TResult> handler,
        ILogger<LoggingQueryHandlerDecorator<TQuery, TResult>> logger)
    {
        _handler = handler;
        _logger = logger;
    }

    public async Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken = default)
    {
        var queryName = typeof(TQuery).Name;
        _logger.LogInformation("Handling query {QueryName}: {@Query}", queryName, query);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await _handler.HandleAsync(query, cancellationToken);
            stopwatch.Stop();

            _logger.LogInformation("Query {QueryName} handled successfully in {ElapsedMs}ms",
                queryName, stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error handling query {QueryName} after {ElapsedMs}ms",
                queryName, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}

[Decorator]
public class ValidationQueryHandlerDecorator<TQuery, TResult> : IQueryHandler<TQuery, TResult>
    where TQuery : class, IQuery<TResult>
{
    private readonly IQueryHandler<TQuery, TResult> _handler;
    private readonly IValidator<TQuery> _validator;

    public ValidationQueryHandlerDecorator(IQueryHandler<TQuery, TResult> handler, IValidator<TQuery> validator)
    {
        _handler = handler;
        _validator = validator;
    }

    public async Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(query, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        return await _handler.HandleAsync(query, cancellationToken);
    }
}
```

### 2. Query Validation

Implement query validation with FluentValidation:

```csharp
public class BrowseUsersQueryValidator : AbstractValidator<BrowseUsersQuery>
{
    public BrowseUsersQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThan(0)
            .WithMessage("Page must be greater than 0");

        RuleFor(x => x.PageSize)
            .GreaterThan(0)
            .LessThanOrEqualTo(100)
            .WithMessage("PageSize must be between 1 and 100");

        RuleFor(x => x.Role)
            .Must(BeValidRole)
            .When(x => !string.IsNullOrEmpty(x.Role))
            .WithMessage("Invalid role specified");

        RuleFor(x => x.CreatedFrom)
            .LessThanOrEqualTo(x => x.CreatedTo)
            .When(x => x.CreatedFrom.HasValue && x.CreatedTo.HasValue)
            .WithMessage("CreatedFrom must be less than or equal to CreatedTo");
    }

    private bool BeValidRole(string role)
    {
        var validRoles = new[] { "Admin", "User", "Manager", "Guest" };
        return validRoles.Contains(role);
    }
}

public class GetUserStatisticsQueryValidator : AbstractValidator<GetUserStatisticsQuery>
{
    public GetUserStatisticsQueryValidator()
    {
        RuleFor(x => x.FromDate)
            .LessThanOrEqualTo(x => x.ToDate)
            .WithMessage("FromDate must be less than or equal to ToDate");

        RuleFor(x => x.ToDate)
            .LessThanOrEqualTo(DateTime.UtcNow)
            .WithMessage("ToDate cannot be in the future");

        RuleFor(x => x.Roles)
            .NotEmpty()
            .WithMessage("At least one role must be specified");
    }
}
```

### 3. Read Models and Projections

Implement optimized read models:

```csharp
public class UserReadModel
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public string Role { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public bool IsActive { get; set; }
    public int LoginCount { get; set; }
    public string Department { get; set; }
    public DateTime LastUpdated { get; set; }
}

public interface IUserReadModelRepository
{
    Task<UserReadModel> GetByIdAsync(Guid id);
    Task<PagedResult<UserReadModel>> BrowseAsync(Expression<Func<UserReadModel, bool>> predicate, IPagedQuery query);
    Task<UserStatisticsDto> GetStatisticsAsync(DateTime fromDate, DateTime toDate, string[] roles);
    Task UpdateAsync(UserReadModel readModel);
    Task DeleteAsync(Guid id);
}

public class UserReadModelRepository : IUserReadModelRepository
{
    private readonly IMongoCollection<UserReadModel> _collection;

    public UserReadModelRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<UserReadModel>("user_read_models");
    }

    public async Task<UserReadModel> GetByIdAsync(Guid id)
    {
        return await _collection.Find(x => x.Id == id).FirstOrDefaultAsync();
    }

    public async Task<PagedResult<UserReadModel>> BrowseAsync(Expression<Func<UserReadModel, bool>> predicate, IPagedQuery query)
    {
        var filter = Builders<UserReadModel>.Filter.Where(predicate);

        var totalItems = await _collection.CountDocumentsAsync(filter);
        var items = await _collection
            .Find(filter)
            .Skip((query.Page - 1) * query.PageSize)
            .Limit(query.PageSize)
            .SortBy(x => x.Name)
            .ToListAsync();

        return new PagedResult<UserReadModel>
        {
            Items = items,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalItems = (int)totalItems,
            TotalPages = (int)Math.Ceiling((double)totalItems / query.PageSize)
        };
    }

    public async Task<UserStatisticsDto> GetStatisticsAsync(DateTime fromDate, DateTime toDate, string[] roles)
    {
        var pipeline = new[]
        {
            new BsonDocument("$match", new BsonDocument
            {
                { "CreatedAt", new BsonDocument { { "$gte", fromDate }, { "$lte", toDate } } },
                { "Role", new BsonDocument("$in", new BsonArray(roles)) }
            }),
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", "$Role" },
                { "total", new BsonDocument("$sum", 1) },
                { "active", new BsonDocument("$sum", new BsonDocument("$cond", new BsonArray { "$IsActive", 1, 0 })) }
            })
        };

        var aggregationResult = await _collection.AggregateAsync<BsonDocument>(pipeline);
        var groupedData = await aggregationResult.ToListAsync();

        // Process aggregation results and build statistics
        return new UserStatisticsDto
        {
            LastCalculated = DateTime.UtcNow
            // ... populate from aggregation results
        };
    }

    public async Task UpdateAsync(UserReadModel readModel)
    {
        readModel.LastUpdated = DateTime.UtcNow;
        await _collection.ReplaceOneAsync(x => x.Id == readModel.Id, readModel, new ReplaceOptions { IsUpsert = true });
    }

    public async Task DeleteAsync(Guid id)
    {
        await _collection.DeleteOneAsync(x => x.Id == id);
    }
}

// Read model projection handlers
public class UserReadModelProjectionHandler :
    IEventHandler<UserCreatedEvent>,
    IEventHandler<UserUpdatedEvent>,
    IEventHandler<UserDeletedEvent>
{
    private readonly IUserReadModelRepository _readModelRepository;

    public UserReadModelProjectionHandler(IUserReadModelRepository readModelRepository)
    {
        _readModelRepository = readModelRepository;
    }

    public async Task HandleAsync(UserCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        var readModel = new UserReadModel
        {
            Id = @event.UserId,
            Name = @event.Name,
            Email = @event.Email,
            Role = @event.Role,
            CreatedAt = @event.CreatedAt,
            IsActive = true,
            LoginCount = 0
        };

        await _readModelRepository.UpdateAsync(readModel);
    }

    public async Task HandleAsync(UserUpdatedEvent @event, CancellationToken cancellationToken = default)
    {
        var readModel = await _readModelRepository.GetByIdAsync(@event.UserId);
        if (readModel != null)
        {
            readModel.Name = @event.Name;
            readModel.Email = @event.Email;
            await _readModelRepository.UpdateAsync(readModel);
        }
    }

    public async Task HandleAsync(UserDeletedEvent @event, CancellationToken cancellationToken = default)
    {
        await _readModelRepository.DeleteAsync(@event.UserId);
    }
}
```

## API Reference

### Interfaces

#### IQuery&lt;TResult&gt;
```csharp
public interface IQuery<TResult>
{
    // Marker interface - no members
}
```

Marker interface that all queries must implement.

#### IQueryHandler&lt;TQuery, TResult&gt;
```csharp
public interface IQueryHandler<in TQuery, TResult> where TQuery : class, IQuery<TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken = default);
}
```

Interface for query handlers.

**Type Parameters:**
- `TQuery` - Type of query to handle
- `TResult` - Type of result to return

**Methods:**
- `HandleAsync()` - Handles the query and returns the result

#### IQueryDispatcher
```csharp
public interface IQueryDispatcher
{
    Task<TResult> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default);
}
```

Interface for dispatching queries to their handlers.

**Methods:**
- `QueryAsync<TResult>()` - Dispatches a query to its handler and returns the result

### Base Classes

#### PagedQueryBase
```csharp
public abstract class PagedQueryBase : IPagedQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string OrderBy { get; set; }
    public string SortOrder { get; set; }
}
```

Base class for paginated queries.

### Extension Methods

#### AddQueryHandlers()
```csharp
public static IConveyBuilder AddQueryHandlers(this IConveyBuilder builder)
```

Registers all query handlers found in loaded assemblies.

#### AddInMemoryQueryDispatcher()
```csharp
public static IConveyBuilder AddInMemoryQueryDispatcher(this IConveyBuilder builder)
```

Registers the in-memory query dispatcher implementation.

## Testing

### Unit Testing Query Handlers

```csharp
public class GetUserQueryHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly GetUserQueryHandler _handler;

    public GetUserQueryHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _handler = new GetUserQueryHandler(_userRepositoryMock.Object, Mock.Of<ILogger<GetUserQueryHandler>>());
    }

    [Fact]
    public async Task HandleAsync_ExistingUser_ReturnsUserDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User("John Doe", "john@example.com") { Id = userId };
        var query = new GetUserQuery(userId);

        _userRepositoryMock.Setup(x => x.GetAsync(userId)).ReturnsAsync(user);

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(userId, result.Id);
        Assert.Equal("John Doe", result.Name);
        Assert.Equal("john@example.com", result.Email);
    }

    [Fact]
    public async Task HandleAsync_NonExistingUser_ReturnsNull()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetUserQuery(userId);

        _userRepositoryMock.Setup(x => x.GetAsync(userId)).ReturnsAsync((User)null);

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        Assert.Null(result);
    }
}
```

### Integration Testing

```csharp
public class QueryIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly IQueryDispatcher _dispatcher;

    public QueryIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _dispatcher = _factory.Services.GetRequiredService<IQueryDispatcher>();
    }

    [Fact]
    public async Task QueryAsync_GetUser_ReturnsCorrectUser()
    {
        // Arrange
        var userId = await SeedTestUserAsync();
        var query = new GetUserQuery(userId);

        // Act
        var result = await _dispatcher.QueryAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(userId, result.Id);
    }

    [Fact]
    public async Task QueryAsync_BrowseUsers_ReturnsPaginatedResults()
    {
        // Arrange
        await SeedMultipleUsersAsync(15);
        var query = new BrowseUsersQuery { Page = 1, PageSize = 10 };

        // Act
        var result = await _dispatcher.QueryAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(10, result.Items.Count);
        Assert.Equal(15, result.TotalItems);
        Assert.Equal(2, result.TotalPages);
    }

    private async Task<Guid> SeedTestUserAsync()
    {
        var userRepository = _factory.Services.GetRequiredService<IUserRepository>();
        var user = new User("Test User", "test@example.com");
        await userRepository.AddAsync(user);
        return user.Id;
    }

    private async Task SeedMultipleUsersAsync(int count)
    {
        var userRepository = _factory.Services.GetRequiredService<IUserRepository>();
        for (int i = 0; i < count; i++)
        {
            var user = new User($"User {i}", $"user{i}@example.com");
            await userRepository.AddAsync(user);
        }
    }
}
```

## Best Practices

1. **Keep queries simple** - Queries should be simple data structures
2. **Use immutable queries** - Make query properties read-only when possible
3. **Optimize for reads** - Design DTOs and read models for optimal performance
4. **Implement caching** - Use caching decorators for frequently accessed data
5. **Use projections** - Return only the data needed by the client
6. **Validate queries** - Implement proper query validation
7. **Handle pagination** - Always implement pagination for list queries
8. **Use meaningful names** - Query names should clearly indicate what data is retrieved

## Troubleshooting

### Common Issues

1. **Handler not found**
   - Ensure handlers implement `IQueryHandler<TQuery, TResult>`
   - Check that `AddQueryHandlers()` is called
   - Verify handler is not marked with `[Decorator]` attribute

2. **Wrong result type**
   - Ensure query implements `IQuery<TResult>` with correct result type
   - Verify handler returns the expected type
   - Check for type mismatches in decorators

3. **Performance issues**
   - Implement proper caching strategies
   - Use database indexes for frequently queried fields
   - Consider read model optimizations
   - Implement query result projections

4. **Validation errors**
   - Ensure validation decorators are properly registered
   - Check validator implementations
   - Verify query validation rules are correct

