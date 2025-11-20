# Convey.Persistence.Redis

Redis persistence integration providing caching, session storage, distributed locking, and high-performance data operations for Convey-based applications with comprehensive Redis features.

## Installation

```bash
dotnet add package Convey.Persistence.Redis
```

## Overview

Convey.Persistence.Redis provides:
- **Redis client integration** - StackExchange.Redis wrapper with Convey patterns
- **Caching abstractions** - Distributed caching with serialization
- **Session storage** - Redis-backed session management
- **Distributed locks** - Concurrency control across instances
- **Pub/Sub messaging** - Redis publish/subscribe patterns
- **Data structures** - Redis sets, lists, hashes, and sorted sets
- **Health checks** - Redis connectivity monitoring
- **Connection pooling** - Efficient connection management

## Configuration

### Basic Redis Setup

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddRedis(redis =>
    {
        redis.ConnectionString = builder.Configuration.GetConnectionString("Redis");
        redis.Database = 0;
        redis.KeyPrefix = "myapp:";
    });

var app = builder.Build();
app.Run();
```

### Advanced Redis Configuration

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddRedis(redis =>
    {
        redis.ConnectionString = builder.Configuration.GetConnectionString("Redis");
        redis.Database = 0;
        redis.KeyPrefix = "myapp:";
        redis.ConnectTimeout = TimeSpan.FromSeconds(5);
        redis.CommandTimeout = TimeSpan.FromSeconds(5);
        redis.ConnectRetry = 3;
        redis.AbortOnConnectFail = false;
        redis.AllowAdmin = false;
        redis.SyncTimeout = TimeSpan.FromSeconds(5);
        redis.AsyncTimeout = TimeSpan.FromSeconds(5);
        redis.KeepAlive = 60;
        redis.DefaultExpiryTime = TimeSpan.FromMinutes(30);
        redis.Serializer = RedisSerializer.SystemTextJson;
        redis.Compression = CompressionType.Gzip;
        redis.HealthCheck = new RedisHealthCheckOptions
        {
            Enabled = true,
            Interval = TimeSpan.FromMinutes(1),
            Timeout = TimeSpan.FromSeconds(5)
        };
        redis.Clustering = new RedisClusteringOptions
        {
            Enabled = false,
            Endpoints = new[] { "redis1:6379", "redis2:6379", "redis3:6379" }
        };
    })
    .AddRedisDistributedCache()
    .AddRedisSession()
    .AddRedisDistributedLock()
    .AddRedisPubSub();

var app = builder.Build();
app.Run();
```

## Key Features

### 1. Caching Operations

High-performance distributed caching:

```csharp
// Redis cache service interface
public interface IRedisCache
{
    Task<T> GetAsync<T>(string key);
    Task<string> GetStringAsync(string key);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null);
    Task SetStringAsync(string key, string value, TimeSpan? expiry = null);
    Task<bool> ExistsAsync(string key);
    Task<bool> DeleteAsync(string key);
    Task<long> DeleteByPatternAsync(string pattern);
    Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null);
    Task<Dictionary<string, T>> GetManyAsync<T>(IEnumerable<string> keys);
    Task SetManyAsync<T>(Dictionary<string, T> keyValues, TimeSpan? expiry = null);
    Task<long> IncrementAsync(string key, long value = 1);
    Task<double> IncrementAsync(string key, double value);
    Task<long> DecrementAsync(string key, long value = 1);
    Task<bool> SetIfNotExistsAsync<T>(string key, T value, TimeSpan? expiry = null);
    Task<TimeSpan?> GetExpiryAsync(string key);
    Task<bool> ExtendExpiryAsync(string key, TimeSpan expiry);
}

// User caching service
public class UserCacheService
{
    private readonly IRedisCache _cache;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<UserCacheService> _logger;
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(30);

    public UserCacheService(
        IRedisCache cache,
        IUserRepository userRepository,
        ILogger<UserCacheService> logger)
    {
        _cache = cache;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<User> GetUserAsync(Guid userId)
    {
        var cacheKey = $"user:{userId}";

        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            _logger.LogDebug("Cache miss for user {UserId}, fetching from database", userId);
            var user = await _userRepository.GetByIdAsync(userId);

            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found", userId);
                return null;
            }

            return user;
        }, CacheExpiry);
    }

    public async Task<IEnumerable<User>> GetUsersAsync(IEnumerable<Guid> userIds)
    {
        var cacheKeys = userIds.Select(id => $"user:{id}").ToList();
        var cachedUsers = await _cache.GetManyAsync<User>(cacheKeys);

        var missingIds = userIds
            .Where(id => !cachedUsers.ContainsKey($"user:{id}") || cachedUsers[$"user:{id}"] == null)
            .ToList();

        if (missingIds.Any())
        {
            _logger.LogDebug("Cache miss for {Count} users, fetching from database", missingIds.Count);
            var dbUsers = await _userRepository.GetByIdsAsync(missingIds);

            var cacheItems = dbUsers.ToDictionary(u => $"user:{u.Id}", u => u);
            await _cache.SetManyAsync(cacheItems, CacheExpiry);

            // Merge cached and DB results
            foreach (var kvp in cacheItems)
            {
                cachedUsers[kvp.Key] = kvp.Value;
            }
        }

        return cachedUsers.Values.Where(u => u != null);
    }

    public async Task SetUserAsync(User user)
    {
        var cacheKey = $"user:{user.Id}";
        await _cache.SetAsync(cacheKey, user, CacheExpiry);
        _logger.LogDebug("Cached user {UserId}", user.Id);
    }

    public async Task InvalidateUserAsync(Guid userId)
    {
        var cacheKey = $"user:{userId}";
        var deleted = await _cache.DeleteAsync(cacheKey);

        if (deleted)
        {
            _logger.LogDebug("Invalidated cache for user {UserId}", userId);
        }
    }

    public async Task InvalidateUsersByRoleAsync(string role)
    {
        var pattern = $"user:*:role:{role}";
        var deletedCount = await _cache.DeleteByPatternAsync(pattern);
        _logger.LogInformation("Invalidated {Count} users with role {Role}", deletedCount, role);
    }

    public async Task<UserStatistics> GetUserStatisticsAsync()
    {
        const string cacheKey = "user:statistics";

        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            _logger.LogDebug("Calculating user statistics");

            var totalUsers = await _userRepository.CountAsync();
            var activeUsers = await _userRepository.CountAsync(u => u.IsActive);
            var newUsersToday = await _userRepository.CountAsync(u => u.CreatedAt.Date == DateTime.Today);

            return new UserStatistics
            {
                TotalUsers = totalUsers,
                ActiveUsers = activeUsers,
                NewUsersToday = newUsersToday,
                InactiveUsers = totalUsers - activeUsers,
                CalculatedAt = DateTime.UtcNow
            };
        }, TimeSpan.FromMinutes(5)); // Shorter expiry for frequently changing data
    }
}

// Session caching service
public class SessionCacheService
{
    private readonly IRedisCache _cache;
    private readonly ILogger<SessionCacheService> _logger;
    private static readonly TimeSpan SessionExpiry = TimeSpan.FromHours(8);

    public SessionCacheService(IRedisCache cache, ILogger<SessionCacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<UserSession> GetSessionAsync(string sessionId)
    {
        var cacheKey = $"session:{sessionId}";
        var session = await _cache.GetAsync<UserSession>(cacheKey);

        if (session != null)
        {
            // Extend session expiry on access
            await _cache.ExtendExpiryAsync(cacheKey, SessionExpiry);
            session.LastAccessedAt = DateTime.UtcNow;
            await _cache.SetAsync(cacheKey, session, SessionExpiry);
        }

        return session;
    }

    public async Task SetSessionAsync(UserSession session)
    {
        var cacheKey = $"session:{session.SessionId}";
        session.LastAccessedAt = DateTime.UtcNow;
        await _cache.SetAsync(cacheKey, session, SessionExpiry);

        // Also maintain user -> sessions mapping for cleanup
        var userSessionsKey = $"user:{session.UserId}:sessions";
        await _cache.SetAddAsync(userSessionsKey, session.SessionId);

        _logger.LogDebug("Created session {SessionId} for user {UserId}", session.SessionId, session.UserId);
    }

    public async Task DeleteSessionAsync(string sessionId)
    {
        var session = await GetSessionAsync(sessionId);
        if (session != null)
        {
            var cacheKey = $"session:{sessionId}";
            await _cache.DeleteAsync(cacheKey);

            // Remove from user sessions set
            var userSessionsKey = $"user:{session.UserId}:sessions";
            await _cache.SetRemoveAsync(userSessionsKey, sessionId);

            _logger.LogDebug("Deleted session {SessionId}", sessionId);
        }
    }

    public async Task DeleteUserSessionsAsync(Guid userId)
    {
        var userSessionsKey = $"user:{userId}:sessions";
        var sessionIds = await _cache.SetMembersAsync<string>(userSessionsKey);

        foreach (var sessionId in sessionIds)
        {
            await _cache.DeleteAsync($"session:{sessionId}");
        }

        await _cache.DeleteAsync(userSessionsKey);

        _logger.LogInformation("Deleted {Count} sessions for user {UserId}", sessionIds.Count(), userId);
    }
}

// Product catalog caching
public class ProductCacheService
{
    private readonly IRedisCache _cache;
    private readonly IProductRepository _productRepository;
    private readonly ILogger<ProductCacheService> _logger;

    public ProductCacheService(
        IRedisCache cache,
        IProductRepository productRepository,
        ILogger<ProductCacheService> logger)
    {
        _cache = cache;
        _productRepository = productRepository;
        _logger = logger;
    }

    public async Task<IEnumerable<Product>> GetFeaturedProductsAsync(int count = 10)
    {
        const string cacheKey = "products:featured";

        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            var products = await _productRepository.GetFeaturedAsync(count);
            _logger.LogDebug("Cached {Count} featured products", products.Count());
            return products;
        }, TimeSpan.FromHours(1));
    }

    public async Task<IEnumerable<Product>> GetProductsByCategoryAsync(string category, int page = 1, int pageSize = 20)
    {
        var cacheKey = $"products:category:{category}:page:{page}:size:{pageSize}";

        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            var products = await _productRepository.GetByCategoryAsync(category, page, pageSize);
            _logger.LogDebug("Cached {Count} products for category {Category}", products.Count(), category);
            return products;
        }, TimeSpan.FromMinutes(30));
    }

    public async Task InvalidateProductCacheAsync(string productId)
    {
        // Invalidate specific product
        await _cache.DeleteAsync($"product:{productId}");

        // Invalidate related caches
        await _cache.DeleteByPatternAsync("products:category:*");
        await _cache.DeleteByPatternAsync("products:featured");
        await _cache.DeleteByPatternAsync("products:search:*");

        _logger.LogDebug("Invalidated product cache for {ProductId}", productId);
    }
}
```

### 2. Distributed Locking

Implement distributed locks for concurrency control:

```csharp
// Distributed lock service interface
public interface IDistributedLock
{
    Task<IDistributedLockHandle> AcquireLockAsync(string key, TimeSpan expiry, TimeSpan? timeout = null);
    Task<bool> ReleaseLockAsync(string key, string lockValue);
    Task<bool> ExtendLockAsync(string key, string lockValue, TimeSpan expiry);
}

// Distributed lock handle
public interface IDistributedLockHandle : IAsyncDisposable
{
    string Key { get; }
    string LockValue { get; }
    TimeSpan Expiry { get; }
    DateTime AcquiredAt { get; }
    bool IsAcquired { get; }
    Task<bool> ExtendAsync(TimeSpan expiry);
    Task ReleaseAsync();
}

// Inventory management with distributed locking
public class InventoryService
{
    private readonly IDistributedLock _distributedLock;
    private readonly IProductRepository _productRepository;
    private readonly IRedisCache _cache;
    private readonly ILogger<InventoryService> _logger;

    public InventoryService(
        IDistributedLock distributedLock,
        IProductRepository productRepository,
        IRedisCache cache,
        ILogger<InventoryService> logger)
    {
        _distributedLock = distributedLock;
        _productRepository = productRepository;
        _cache = cache;
        _logger = logger;
    }

    public async Task<bool> ReserveInventoryAsync(string productId, int quantity)
    {
        var lockKey = $"inventory:lock:{productId}";
        var lockExpiry = TimeSpan.FromMinutes(5);
        var timeout = TimeSpan.FromSeconds(10);

        await using var lockHandle = await _distributedLock.AcquireLockAsync(lockKey, lockExpiry, timeout);

        if (!lockHandle.IsAcquired)
        {
            _logger.LogWarning("Failed to acquire lock for product {ProductId}", productId);
            return false;
        }

        try
        {
            // Get current inventory
            var inventoryKey = $"inventory:{productId}";
            var currentInventory = await _cache.GetAsync<int?>(inventoryKey);

            if (currentInventory == null)
            {
                // Load from database if not in cache
                var product = await _productRepository.GetByIdAsync(productId);
                if (product == null)
                {
                    _logger.LogWarning("Product {ProductId} not found", productId);
                    return false;
                }

                currentInventory = product.Inventory;
                await _cache.SetAsync(inventoryKey, currentInventory, TimeSpan.FromHours(24));
            }

            // Check if sufficient inventory
            if (currentInventory < quantity)
            {
                _logger.LogWarning("Insufficient inventory for product {ProductId}. Available: {Available}, Requested: {Requested}",
                    productId, currentInventory, quantity);
                return false;
            }

            // Reserve inventory
            var newInventory = currentInventory.Value - quantity;
            await _cache.SetAsync(inventoryKey, newInventory, TimeSpan.FromHours(24));

            // Update database
            await _productRepository.UpdateInventoryAsync(productId, newInventory);

            _logger.LogInformation("Reserved {Quantity} units of product {ProductId}. New inventory: {NewInventory}",
                quantity, productId, newInventory);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reserving inventory for product {ProductId}", productId);
            return false;
        }
    }

    public async Task<bool> ReleaseInventoryAsync(string productId, int quantity)
    {
        var lockKey = $"inventory:lock:{productId}";
        var lockExpiry = TimeSpan.FromMinutes(5);

        await using var lockHandle = await _distributedLock.AcquireLockAsync(lockKey, lockExpiry);

        if (!lockHandle.IsAcquired)
        {
            _logger.LogWarning("Failed to acquire lock for product {ProductId}", productId);
            return false;
        }

        try
        {
            var inventoryKey = $"inventory:{productId}";
            var currentInventory = await _cache.GetAsync<int?>(inventoryKey);

            if (currentInventory == null)
            {
                var product = await _productRepository.GetByIdAsync(productId);
                currentInventory = product?.Inventory ?? 0;
            }

            var newInventory = currentInventory.Value + quantity;
            await _cache.SetAsync(inventoryKey, newInventory, TimeSpan.FromHours(24));
            await _productRepository.UpdateInventoryAsync(productId, newInventory);

            _logger.LogInformation("Released {Quantity} units of product {ProductId}. New inventory: {NewInventory}",
                quantity, productId, newInventory);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing inventory for product {ProductId}", productId);
            return false;
        }
    }
}

// Order processing with distributed coordination
public class OrderProcessingService
{
    private readonly IDistributedLock _distributedLock;
    private readonly IOrderRepository _orderRepository;
    private readonly ILogger<OrderProcessingService> _logger;

    public OrderProcessingService(
        IDistributedLock distributedLock,
        IOrderRepository orderRepository,
        ILogger<OrderProcessingService> logger)
    {
        _distributedLock = distributedLock;
        _orderRepository = orderRepository;
        _logger = logger;
    }

    public async Task<bool> ProcessOrderAsync(string orderId)
    {
        var lockKey = $"order:processing:{orderId}";
        var lockExpiry = TimeSpan.FromMinutes(10);

        await using var lockHandle = await _distributedLock.AcquireLockAsync(lockKey, lockExpiry);

        if (!lockHandle.IsAcquired)
        {
            _logger.LogWarning("Order {OrderId} is already being processed", orderId);
            return false;
        }

        try
        {
            var order = await _orderRepository.GetByIdAsync(orderId);
            if (order == null)
            {
                _logger.LogWarning("Order {OrderId} not found", orderId);
                return false;
            }

            if (order.Status != OrderStatus.Pending)
            {
                _logger.LogWarning("Order {OrderId} is not in pending status: {Status}", orderId, order.Status);
                return false;
            }

            // Extend lock for longer processing
            await lockHandle.ExtendAsync(TimeSpan.FromMinutes(15));

            // Process order steps
            await ProcessPaymentAsync(order);
            await ReserveInventoryAsync(order);
            await CreateShipmentAsync(order);

            order.Status = OrderStatus.Processing;
            order.ProcessedAt = DateTime.UtcNow;
            await _orderRepository.UpdateAsync(order);

            _logger.LogInformation("Successfully processed order {OrderId}", orderId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing order {OrderId}", orderId);
            return false;
        }
    }

    private async Task ProcessPaymentAsync(Order order)
    {
        // Payment processing logic
        await Task.Delay(1000); // Simulate payment processing
    }

    private async Task ReserveInventoryAsync(Order order)
    {
        // Inventory reservation logic
        await Task.Delay(500); // Simulate inventory check
    }

    private async Task CreateShipmentAsync(Order order)
    {
        // Shipment creation logic
        await Task.Delay(800); // Simulate shipment creation
    }
}

// Singleton pattern with distributed lock
public class ConfigurationService
{
    private readonly IDistributedLock _distributedLock;
    private readonly IRedisCache _cache;
    private readonly IConfigurationRepository _configRepository;
    private readonly ILogger<ConfigurationService> _logger;
    private static readonly object LocalLock = new object();

    public ConfigurationService(
        IDistributedLock distributedLock,
        IRedisCache cache,
        IConfigurationRepository configRepository,
        ILogger<ConfigurationService> logger)
    {
        _distributedLock = distributedLock;
        _cache = cache;
        _configRepository = configRepository;
        _logger = logger;
    }

    public async Task<string> GetConfigurationAsync(string key)
    {
        var cacheKey = $"config:{key}";
        var cachedValue = await _cache.GetStringAsync(cacheKey);

        if (cachedValue != null)
        {
            return cachedValue;
        }

        // Use distributed lock to prevent multiple instances from loading the same config
        var lockKey = $"config:load:{key}";
        var lockExpiry = TimeSpan.FromMinutes(2);

        await using var lockHandle = await _distributedLock.AcquireLockAsync(lockKey, lockExpiry);

        if (lockHandle.IsAcquired)
        {
            // Double-check cache after acquiring lock
            cachedValue = await _cache.GetStringAsync(cacheKey);
            if (cachedValue != null)
            {
                return cachedValue;
            }

            // Load from database
            var dbValue = await _configRepository.GetAsync(key);
            if (dbValue != null)
            {
                await _cache.SetStringAsync(cacheKey, dbValue, TimeSpan.FromHours(1));
                _logger.LogDebug("Loaded configuration {Key} from database", key);
            }

            return dbValue;
        }

        // If we couldn't acquire the lock, try cache one more time
        // Another instance might have loaded it
        await Task.Delay(100);
        return await _cache.GetStringAsync(cacheKey);
    }
}
```

### 3. Redis Data Structures

Utilize Redis advanced data structures:

```csharp
// Redis data structures service
public interface IRedisDataStructures
{
    // Sets
    Task<bool> SetAddAsync<T>(string key, T value);
    Task<long> SetAddAsync<T>(string key, IEnumerable<T> values);
    Task<bool> SetRemoveAsync<T>(string key, T value);
    Task<IEnumerable<T>> SetMembersAsync<T>(string key);
    Task<bool> SetContainsAsync<T>(string key, T value);
    Task<long> SetCountAsync(string key);

    // Lists
    Task<long> ListPushAsync<T>(string key, T value, bool leftSide = true);
    Task<T> ListPopAsync<T>(string key, bool leftSide = true);
    Task<IEnumerable<T>> ListRangeAsync<T>(string key, long start = 0, long stop = -1);
    Task<long> ListLengthAsync(string key);

    // Hashes
    Task<bool> HashSetAsync<T>(string key, string field, T value);
    Task HashSetAsync<T>(string key, Dictionary<string, T> values);
    Task<T> HashGetAsync<T>(string key, string field);
    Task<Dictionary<string, T>> HashGetAllAsync<T>(string key);
    Task<bool> HashDeleteAsync(string key, string field);
    Task<bool> HashExistsAsync(string key, string field);

    // Sorted Sets
    Task<bool> SortedSetAddAsync<T>(string key, T value, double score);
    Task<long> SortedSetAddAsync<T>(string key, Dictionary<T, double> values);
    Task<IEnumerable<T>> SortedSetRangeAsync<T>(string key, long start = 0, long stop = -1, bool descending = false);
    Task<IEnumerable<T>> SortedSetRangeByScoreAsync<T>(string key, double min, double max);
    Task<double?> SortedSetScoreAsync<T>(string key, T value);
    Task<long> SortedSetRankAsync<T>(string key, T value, bool descending = false);
}

// User activity tracking with Redis sets
public class UserActivityService
{
    private readonly IRedisDataStructures _redis;
    private readonly ILogger<UserActivityService> _logger;

    public UserActivityService(IRedisDataStructures redis, ILogger<UserActivityService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task TrackUserActivityAsync(Guid userId, string activity)
    {
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        var activityKey = $"activity:{today}:{activity}";

        await _redis.SetAddAsync(activityKey, userId.ToString());

        // Set expiry for the activity set (cleanup old data)
        await _redis.SetExpiryAsync(activityKey, TimeSpan.FromDays(30));

        _logger.LogDebug("Tracked activity {Activity} for user {UserId}", activity, userId);
    }

    public async Task<IEnumerable<Guid>> GetActiveUsersAsync(DateTime date, string activity)
    {
        var dateKey = date.ToString("yyyy-MM-dd");
        var activityKey = $"activity:{dateKey}:{activity}";

        var userIds = await _redis.SetMembersAsync<string>(activityKey);
        return userIds.Select(Guid.Parse);
    }

    public async Task<long> GetActiveUserCountAsync(DateTime date, string activity)
    {
        var dateKey = date.ToString("yyyy-MM-dd");
        var activityKey = $"activity:{dateKey}:{activity}";

        return await _redis.SetCountAsync(activityKey);
    }

    public async Task<Dictionary<string, long>> GetDailyActivitySummaryAsync(DateTime date)
    {
        var dateKey = date.ToString("yyyy-MM-dd");
        var activities = new[] { "login", "page_view", "purchase", "upload", "download" };

        var summary = new Dictionary<string, long>();

        foreach (var activity in activities)
        {
            var activityKey = $"activity:{dateKey}:{activity}";
            var count = await _redis.SetCountAsync(activityKey);
            summary[activity] = count;
        }

        return summary;
    }
}

// Real-time leaderboard with sorted sets
public class LeaderboardService
{
    private readonly IRedisDataStructures _redis;
    private readonly ILogger<LeaderboardService> _logger;

    public LeaderboardService(IRedisDataStructures redis, ILogger<LeaderboardService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task UpdateScoreAsync(string leaderboard, string playerId, double score)
    {
        var leaderboardKey = $"leaderboard:{leaderboard}";
        await _redis.SortedSetAddAsync(leaderboardKey, playerId, score);

        _logger.LogDebug("Updated score for player {PlayerId} in leaderboard {Leaderboard}: {Score}",
            playerId, leaderboard, score);
    }

    public async Task<IEnumerable<LeaderboardEntry>> GetTopPlayersAsync(string leaderboard, int count = 10)
    {
        var leaderboardKey = $"leaderboard:{leaderboard}";
        var players = await _redis.SortedSetRangeAsync<string>(leaderboardKey, 0, count - 1, descending: true);

        var entries = new List<LeaderboardEntry>();
        var rank = 1;

        foreach (var player in players)
        {
            var score = await _redis.SortedSetScoreAsync<string>(leaderboardKey, player);
            entries.Add(new LeaderboardEntry
            {
                PlayerId = player,
                Score = score ?? 0,
                Rank = rank++
            });
        }

        return entries;
    }

    public async Task<LeaderboardEntry> GetPlayerRankAsync(string leaderboard, string playerId)
    {
        var leaderboardKey = $"leaderboard:{leaderboard}";
        var score = await _redis.SortedSetScoreAsync<string>(leaderboardKey, playerId);

        if (!score.HasValue)
        {
            return null;
        }

        var rank = await _redis.SortedSetRankAsync<string>(leaderboardKey, playerId, descending: true);

        return new LeaderboardEntry
        {
            PlayerId = playerId,
            Score = score.Value,
            Rank = rank + 1 // Redis rank is 0-based
        };
    }

    public async Task<IEnumerable<LeaderboardEntry>> GetPlayersAroundAsync(string leaderboard, string playerId, int range = 5)
    {
        var leaderboardKey = $"leaderboard:{leaderboard}";
        var playerRank = await _redis.SortedSetRankAsync<string>(leaderboardKey, playerId, descending: true);

        if (playerRank < 0)
        {
            return Enumerable.Empty<LeaderboardEntry>();
        }

        var start = Math.Max(0, playerRank - range);
        var stop = playerRank + range;

        var players = await _redis.SortedSetRangeAsync<string>(leaderboardKey, start, stop, descending: true);

        var entries = new List<LeaderboardEntry>();
        var rank = start + 1;

        foreach (var player in players)
        {
            var score = await _redis.SortedSetScoreAsync<string>(leaderboardKey, player);
            entries.Add(new LeaderboardEntry
            {
                PlayerId = player,
                Score = score ?? 0,
                Rank = rank++
            });
        }

        return entries;
    }
}

// Shopping cart with Redis hashes
public class ShoppingCartService
{
    private readonly IRedisDataStructures _redis;
    private readonly ILogger<ShoppingCartService> _logger;
    private static readonly TimeSpan CartExpiry = TimeSpan.FromDays(7);

    public ShoppingCartService(IRedisDataStructures redis, ILogger<ShoppingCartService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task AddItemAsync(Guid userId, string productId, int quantity)
    {
        var cartKey = $"cart:{userId}";
        var currentQuantity = await _redis.HashGetAsync<int?>(cartKey, productId) ?? 0;
        var newQuantity = currentQuantity + quantity;

        await _redis.HashSetAsync(cartKey, productId, newQuantity);
        await _redis.SetExpiryAsync(cartKey, CartExpiry);

        _logger.LogDebug("Added {Quantity} of product {ProductId} to cart for user {UserId}",
            quantity, productId, userId);
    }

    public async Task RemoveItemAsync(Guid userId, string productId)
    {
        var cartKey = $"cart:{userId}";
        await _redis.HashDeleteAsync(cartKey, productId);

        _logger.LogDebug("Removed product {ProductId} from cart for user {UserId}", productId, userId);
    }

    public async Task UpdateItemQuantityAsync(Guid userId, string productId, int quantity)
    {
        var cartKey = $"cart:{userId}";

        if (quantity <= 0)
        {
            await _redis.HashDeleteAsync(cartKey, productId);
        }
        else
        {
            await _redis.HashSetAsync(cartKey, productId, quantity);
            await _redis.SetExpiryAsync(cartKey, CartExpiry);
        }

        _logger.LogDebug("Updated quantity of product {ProductId} to {Quantity} in cart for user {UserId}",
            productId, quantity, userId);
    }

    public async Task<Dictionary<string, int>> GetCartItemsAsync(Guid userId)
    {
        var cartKey = $"cart:{userId}";
        var items = await _redis.HashGetAllAsync<int>(cartKey);

        // Extend cart expiry on access
        if (items.Any())
        {
            await _redis.SetExpiryAsync(cartKey, CartExpiry);
        }

        return items;
    }

    public async Task<int> GetItemCountAsync(Guid userId)
    {
        var items = await GetCartItemsAsync(userId);
        return items.Values.Sum();
    }

    public async Task ClearCartAsync(Guid userId)
    {
        var cartKey = $"cart:{userId}";
        await _redis.DeleteAsync(cartKey);

        _logger.LogDebug("Cleared cart for user {UserId}", userId);
    }
}

// Recent items tracking with Redis lists
public class RecentItemsService
{
    private readonly IRedisDataStructures _redis;
    private readonly ILogger<RecentItemsService> _logger;
    private const int MaxRecentItems = 20;

    public RecentItemsService(IRedisDataStructures redis, ILogger<RecentItemsService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task AddRecentItemAsync(Guid userId, string itemId, string itemType)
    {
        var recentKey = $"recent:{userId}:{itemType}";
        var item = new RecentItem { Id = itemId, ViewedAt = DateTime.UtcNow };

        // Remove item if it already exists (to move it to front)
        await _redis.ListRemoveAsync(recentKey, item);

        // Add to front of list
        await _redis.ListPushAsync(recentKey, item, leftSide: true);

        // Trim list to max size
        await _redis.ListTrimAsync(recentKey, 0, MaxRecentItems - 1);

        // Set expiry
        await _redis.SetExpiryAsync(recentKey, TimeSpan.FromDays(30));

        _logger.LogDebug("Added recent {ItemType} {ItemId} for user {UserId}", itemType, itemId, userId);
    }

    public async Task<IEnumerable<RecentItem>> GetRecentItemsAsync(Guid userId, string itemType, int count = 10)
    {
        var recentKey = $"recent:{userId}:{itemType}";
        var items = await _redis.ListRangeAsync<RecentItem>(recentKey, 0, count - 1);

        return items;
    }

    public async Task ClearRecentItemsAsync(Guid userId, string itemType)
    {
        var recentKey = $"recent:{userId}:{itemType}";
        await _redis.DeleteAsync(recentKey);

        _logger.LogDebug("Cleared recent {ItemType} for user {UserId}", itemType, userId);
    }
}

// Models
public class LeaderboardEntry
{
    public string PlayerId { get; set; }
    public double Score { get; set; }
    public long Rank { get; set; }
}

public class RecentItem
{
    public string Id { get; set; }
    public DateTime ViewedAt { get; set; }
}

public class UserStatistics
{
    public long TotalUsers { get; set; }
    public long ActiveUsers { get; set; }
    public long InactiveUsers { get; set; }
    public long NewUsersToday { get; set; }
    public DateTime CalculatedAt { get; set; }
}

public class UserSession
{
    public string SessionId { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastAccessedAt { get; set; }
    public string IpAddress { get; set; }
    public string UserAgent { get; set; }
    public Dictionary<string, string> Data { get; set; } = new();
}
```

### 4. Redis Pub/Sub Messaging

Implement publish/subscribe patterns:

```csharp
// Redis pub/sub service interface
public interface IRedisPubSub
{
    Task PublishAsync<T>(string channel, T message);
    Task SubscribeAsync<T>(string channel, Func<string, T, Task> handler);
    Task UnsubscribeAsync(string channel);
    Task PatternSubscribeAsync<T>(string pattern, Func<string, string, T, Task> handler);
    Task PatternUnsubscribeAsync(string pattern);
}

// Real-time notification service
public class NotificationService
{
    private readonly IRedisPubSub _pubSub;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(IRedisPubSub pubSub, ILogger<NotificationService> logger)
    {
        _pubSub = pubSub;
        _logger = logger;
    }

    public async Task SendUserNotificationAsync(Guid userId, Notification notification)
    {
        var channel = $"notifications:user:{userId}";
        await _pubSub.PublishAsync(channel, notification);

        _logger.LogDebug("Sent notification to user {UserId}: {Type}", userId, notification.Type);
    }

    public async Task SendBroadcastNotificationAsync(Notification notification)
    {
        const string channel = "notifications:broadcast";
        await _pubSub.PublishAsync(channel, notification);

        _logger.LogInformation("Sent broadcast notification: {Type}", notification.Type);
    }

    public async Task SendRoleNotificationAsync(string role, Notification notification)
    {
        var channel = $"notifications:role:{role}";
        await _pubSub.PublishAsync(channel, notification);

        _logger.LogDebug("Sent notification to role {Role}: {Type}", role, notification.Type);
    }

    public async Task SubscribeToUserNotificationsAsync(Guid userId, Func<Notification, Task> handler)
    {
        var channel = $"notifications:user:{userId}";
        await _pubSub.SubscribeAsync<Notification>(channel, async (ch, notification) =>
        {
            try
            {
                await handler(notification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling notification for user {UserId}", userId);
            }
        });
    }

    public async Task SubscribeToBroadcastNotificationsAsync(Func<Notification, Task> handler)
    {
        const string channel = "notifications:broadcast";
        await _pubSub.SubscribeAsync<Notification>(channel, async (ch, notification) =>
        {
            try
            {
                await handler(notification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling broadcast notification");
            }
        });
    }
}

// Real-time chat service
public class ChatService
{
    private readonly IRedisPubSub _pubSub;
    private readonly IChatRepository _chatRepository;
    private readonly ILogger<ChatService> _logger;

    public ChatService(IRedisPubSub pubSub, IChatRepository chatRepository, ILogger<ChatService> logger)
    {
        _pubSub = pubSub;
        _chatRepository = chatRepository;
        _logger = logger;
    }

    public async Task SendMessageAsync(ChatMessage message)
    {
        // Save to database
        await _chatRepository.SaveMessageAsync(message);

        // Publish to channel
        var channel = $"chat:room:{message.RoomId}";
        await _pubSub.PublishAsync(channel, message);

        _logger.LogDebug("Sent message to room {RoomId} from user {UserId}", message.RoomId, message.SenderId);
    }

    public async Task SubscribeToRoomAsync(string roomId, Func<ChatMessage, Task> handler)
    {
        var channel = $"chat:room:{roomId}";
        await _pubSub.SubscribeAsync<ChatMessage>(channel, async (ch, message) =>
        {
            try
            {
                await handler(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling chat message in room {RoomId}", roomId);
            }
        });
    }

    public async Task JoinRoomAsync(string roomId, Guid userId)
    {
        var channel = $"chat:room:{roomId}:events";
        var joinEvent = new ChatEvent
        {
            Type = ChatEventType.UserJoined,
            UserId = userId,
            RoomId = roomId,
            Timestamp = DateTime.UtcNow
        };

        await _pubSub.PublishAsync(channel, joinEvent);
        _logger.LogDebug("User {UserId} joined room {RoomId}", userId, roomId);
    }

    public async Task LeaveRoomAsync(string roomId, Guid userId)
    {
        var channel = $"chat:room:{roomId}:events";
        var leaveEvent = new ChatEvent
        {
            Type = ChatEventType.UserLeft,
            UserId = userId,
            RoomId = roomId,
            Timestamp = DateTime.UtcNow
        };

        await _pubSub.PublishAsync(channel, leaveEvent);
        _logger.LogDebug("User {UserId} left room {RoomId}", userId, roomId);
    }
}

// Live data synchronization
public class LiveDataSyncService
{
    private readonly IRedisPubSub _pubSub;
    private readonly ILogger<LiveDataSyncService> _logger;

    public LiveDataSyncService(IRedisPubSub pubSub, ILogger<LiveDataSyncService> logger)
    {
        _pubSub = pubSub;
        _logger = logger;
    }

    public async Task PublishDataChangeAsync<T>(string entityType, string operation, T data)
    {
        var changeEvent = new DataChangeEvent<T>
        {
            EntityType = entityType,
            Operation = operation,
            Data = data,
            Timestamp = DateTime.UtcNow,
            Source = Environment.MachineName
        };

        var channel = $"data:changes:{entityType}";
        await _pubSub.PublishAsync(channel, changeEvent);

        _logger.LogDebug("Published {Operation} change for {EntityType}", operation, entityType);
    }

    public async Task SubscribeToDataChangesAsync<T>(string entityType, Func<DataChangeEvent<T>, Task> handler)
    {
        var channel = $"data:changes:{entityType}";
        await _pubSub.SubscribeAsync<DataChangeEvent<T>>(channel, async (ch, changeEvent) =>
        {
            try
            {
                // Don't process changes from same source
                if (changeEvent.Source != Environment.MachineName)
                {
                    await handler(changeEvent);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling data change for {EntityType}", entityType);
            }
        });
    }

    public async Task SubscribeToAllDataChangesAsync(Func<string, string, object, Task> handler)
    {
        await _pubSub.PatternSubscribeAsync<object>("data:changes:*", async (pattern, channel, data) =>
        {
            try
            {
                var entityType = channel.Split(':').LastOrDefault();
                await handler(entityType, channel, data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling data change pattern");
            }
        });
    }
}

// Models
public class Notification
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Type { get; set; }
    public string Title { get; set; }
    public string Message { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Priority { get; set; } = "Normal";
}

public class ChatMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string RoomId { get; set; }
    public Guid SenderId { get; set; }
    public string SenderName { get; set; }
    public string Content { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public ChatMessageType Type { get; set; } = ChatMessageType.Text;
}

public class ChatEvent
{
    public ChatEventType Type { get; set; }
    public Guid UserId { get; set; }
    public string RoomId { get; set; }
    public DateTime Timestamp { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
}

public class DataChangeEvent<T>
{
    public string EntityType { get; set; }
    public string Operation { get; set; }
    public T Data { get; set; }
    public DateTime Timestamp { get; set; }
    public string Source { get; set; }
}

public enum ChatMessageType
{
    Text,
    Image,
    File,
    System
}

public enum ChatEventType
{
    UserJoined,
    UserLeft,
    RoomCreated,
    RoomDeleted
}
```

## Configuration Options

### Redis Options

```csharp
public class RedisOptions
{
    public string ConnectionString { get; set; }
    public int Database { get; set; } = 0;
    public string KeyPrefix { get; set; }
    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromSeconds(5);
    public int ConnectRetry { get; set; } = 3;
    public bool AbortOnConnectFail { get; set; } = false;
    public bool AllowAdmin { get; set; } = false;
    public TimeSpan SyncTimeout { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan AsyncTimeout { get; set; } = TimeSpan.FromSeconds(5);
    public int KeepAlive { get; set; } = 60;
    public TimeSpan DefaultExpiryTime { get; set; } = TimeSpan.FromMinutes(30);
    public RedisSerializer Serializer { get; set; } = RedisSerializer.SystemTextJson;
    public CompressionType Compression { get; set; } = CompressionType.None;
    public RedisHealthCheckOptions HealthCheck { get; set; } = new();
    public RedisClusteringOptions Clustering { get; set; } = new();
}

public enum RedisSerializer
{
    SystemTextJson,
    NewtonsoftJson,
    MessagePack,
    Protobuf
}

public enum CompressionType
{
    None,
    Gzip,
    Deflate,
    Brotli
}
```

## API Reference

### Extension Methods

```csharp
public static class ConveyExtensions
{
    public static IConveyBuilder AddRedis(this IConveyBuilder builder, Action<RedisOptions> configure = null);
    public static IConveyBuilder AddRedisDistributedCache(this IConveyBuilder builder);
    public static IConveyBuilder AddRedisSession(this IConveyBuilder builder);
    public static IConveyBuilder AddRedisDistributedLock(this IConveyBuilder builder);
    public static IConveyBuilder AddRedisPubSub(this IConveyBuilder builder);
}
```

## Best Practices

1. **Use connection pooling** - Reuse Redis connections efficiently
2. **Set appropriate expiration** - Use TTL to prevent memory issues
3. **Handle connection failures** - Implement proper retry logic
4. **Use Redis patterns** - Leverage Redis data structures appropriately
5. **Monitor performance** - Track Redis metrics and performance
6. **Implement proper serialization** - Choose efficient serialization methods
7. **Use Redis clustering** - Scale horizontally when needed
8. **Secure Redis access** - Use authentication and encryption

## Troubleshooting

### Common Issues

1. **Connection timeouts**
   - Check network connectivity
   - Verify Redis server status
   - Adjust timeout settings

2. **Memory issues**
   - Implement proper key expiration
   - Monitor Redis memory usage
   - Use appropriate data structures

3. **Performance problems**
   - Optimize Redis commands
   - Use pipelining for bulk operations
   - Monitor slow queries

4. **Serialization errors**
   - Check object serialization compatibility
   - Verify JSON serialization settings
   - Handle circular references
