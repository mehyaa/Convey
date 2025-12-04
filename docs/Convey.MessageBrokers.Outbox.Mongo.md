---
layout: default
title: Convey.MessageBrokers.Outbox.Mongo
parent: Message Brokers
---
# Convey.MessageBrokers.Outbox.Mongo

MongoDB integration for the Outbox pattern providing NoSQL-based outbox message storage with MongoDB's rich querying capabilities, scalability, and document-based storage.

## Installation

```bash
dotnet add package Convey.MessageBrokers.Outbox.Mongo
```

## Overview

Convey.MessageBrokers.Outbox.Mongo provides:
- **MongoDB integration** - Native MongoDB outbox implementation
- **Document storage** - Flexible document-based message storage
- **Scalability** - Horizontal scaling with MongoDB sharding
- **Rich queries** - MongoDB aggregation and query capabilities
- **High availability** - MongoDB replica set support
- **Atomic operations** - Multi-document transactions for consistency
- **Indexing** - Optimized indexes for message processing
- **GridFS support** - Large message storage capabilities

## Configuration

### Basic MongoDB Outbox Setup

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddMongo()
    .AddRabbitMq()
    .AddOutbox()
    .AddMongoOutbox();

var app = builder.Build();
app.Run();
```

### Advanced MongoDB Configuration

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddMongo(mongo =>
    {
        mongo.ConnectionString = "mongodb://localhost:27017";
        mongo.Database = "ConveyApp";
        mongo.Seed = false;
    })
    .AddRabbitMq()
    .AddOutbox(outbox =>
    {
        outbox.Enabled = true;
        outbox.Type = "sequential";
        outbox.ExpireAfter = TimeSpan.FromDays(7);
        outbox.IntervalMilliseconds = 1000;
        outbox.MaxRetries = 3;
    })
    .AddMongoOutbox(mongo =>
    {
        mongo.CollectionName = "outbox_messages";
        mongo.DatabaseName = "messaging";
        mongo.EnableSharding = true;
        mongo.ShardKey = "_id";
        mongo.MaxDocumentSize = 16777216; // 16MB
    });

var app = builder.Build();
app.Run();
```

### Cluster Configuration

```csharp
builder.Services.AddConvey()
    .AddMongo(mongo =>
    {
        mongo.ConnectionString = "mongodb://mongo1:27017,mongo2:27017,mongo3:27017/?replicaSet=rs0";
        mongo.Database = "ConveyApp";
        mongo.ReadConcern = ReadConcern.Majority;
        mongo.WriteConcern = WriteConcern.WMajority;
        mongo.ReadPreference = ReadPreference.SecondaryPreferred;
    })
    .AddRabbitMq()
    .AddOutbox()
    .AddMongoOutbox(mongo =>
    {
        mongo.CollectionName = "outbox_messages";
        mongo.EnableTransactions = true;
        mongo.TransactionOptions = new TransactionOptions(
            readConcern: ReadConcern.Snapshot,
            writeConcern: WriteConcern.WMajority);
    });
```

## Key Features

### 1. MongoDB Document Model

Define outbox message documents:

```csharp
// MongoDB outbox message document
[BsonIgnoreExtraElements]
public class MongoOutboxMessage
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string Id { get; set; }

    [BsonElement("type")]
    public string Type { get; set; }

    [BsonElement("data")]
    public BsonDocument Data { get; set; }

    [BsonElement("correlationId")]
    public string CorrelationId { get; set; }

    [BsonElement("userId")]
    public string UserId { get; set; }

    [BsonElement("tenantId")]
    public string TenantId { get; set; }

    [BsonElement("status")]
    [BsonRepresentation(BsonType.String)]
    public OutboxMessageStatus Status { get; set; }

    [BsonElement("createdAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; }

    [BsonElement("processedAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? ProcessedAt { get; set; }

    [BsonElement("attempts")]
    public int Attempts { get; set; }

    [BsonElement("error")]
    public string Error { get; set; }

    [BsonElement("metadata")]
    public BsonDocument Metadata { get; set; }

    [BsonElement("partitionKey")]
    public string PartitionKey { get; set; }

    [BsonElement("priority")]
    public int Priority { get; set; } = 0;

    [BsonElement("scheduledAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? ScheduledAt { get; set; }

    [BsonElement("ttl")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? Ttl { get; set; }
}

// Message serialization helpers
public static class MessageSerializationExtensions
{
    public static BsonDocument ToBsonDocument<T>(this T message) where T : class
    {
        var json = JsonSerializer.Serialize(message, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        return BsonDocument.Parse(json);
    }

    public static T FromBsonDocument<T>(this BsonDocument document) where T : class
    {
        var json = document.ToJson();
        return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        });
    }

    public static object FromBsonDocument(this BsonDocument document, Type type)
    {
        var json = document.ToJson();
        return JsonSerializer.Deserialize(json, type, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        });
    }
}
```

### 2. Service Implementation with MongoDB

Use MongoDB outbox in your services:

```csharp
// User service with MongoDB outbox
public class UserService
{
    private readonly IMongoDatabase _database;
    private readonly IMessageOutbox _messageOutbox;
    private readonly IMongoCollection<User> _usersCollection;
    private readonly ILogger<UserService> _logger;

    public UserService(
        IMongoDatabase database,
        IMessageOutbox messageOutbox,
        ILogger<UserService> logger)
    {
        _database = database;
        _messageOutbox = messageOutbox;
        _usersCollection = database.GetCollection<User>("users");
        _logger = logger;
    }

    public async Task<User> CreateUserAsync(CreateUserRequest request)
    {
        using var session = await _database.Client.StartSessionAsync();
        session.StartTransaction();

        try
        {
            _logger.LogInformation("Creating user {Email}", request.Email);

            // Check if user exists
            var existingUser = await _usersCollection
                .Find(session, u => u.Email == request.Email && u.IsActive)
                .FirstOrDefaultAsync();

            if (existingUser != null)
            {
                throw new InvalidOperationException($"User with email {request.Email} already exists");
            }

            // Create user document
            var user = new User
            {
                Id = Guid.NewGuid().ToString(),
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                DateOfBirth = request.DateOfBirth,
                PhoneNumber = request.PhoneNumber,
                Role = request.Role,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                Version = 1
            };

            // Insert user
            await _usersCollection.InsertOneAsync(session, user);
            _logger.LogDebug("User {UserId} saved to MongoDB", user.Id);

            // Create outbox messages within the same transaction
            var userCreatedEvent = new UserCreated
            {
                Id = Guid.Parse(user.Id),
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Role = user.Role,
                CreatedAt = user.CreatedAt
            };

            await _messageOutbox.PublishAsync(userCreatedEvent);

            var welcomeEmailEvent = new SendWelcomeEmail
            {
                UserId = Guid.Parse(user.Id),
                RecipientEmail = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName
            };

            await _messageOutbox.PublishAsync(welcomeEmailEvent);

            // Commit transaction
            await session.CommitTransactionAsync();
            _logger.LogInformation("User {UserId} created successfully with outbox messages", user.Id);

            return user;
        }
        catch (Exception ex)
        {
            await session.AbortTransactionAsync();
            _logger.LogError(ex, "Error creating user {Email}, transaction aborted", request.Email);
            throw;
        }
    }

    public async Task<User> UpdateUserAsync(string userId, UpdateUserRequest request)
    {
        using var session = await _database.Client.StartSessionAsync();
        session.StartTransaction();

        try
        {
            _logger.LogInformation("Updating user {UserId}", userId);

            var filter = Builders<User>.Filter.And(
                Builders<User>.Filter.Eq(u => u.Id, userId),
                Builders<User>.Filter.Eq(u => u.IsActive, true)
            );

            var user = await _usersCollection
                .Find(session, filter)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                throw new NotFoundException($"User {userId} not found");
            }

            // Store original values
            var originalEmail = user.Email;
            var originalFirstName = user.FirstName;
            var originalLastName = user.LastName;

            // Update user properties
            var updateBuilder = Builders<User>.Update
                .Set(u => u.UpdatedAt, DateTime.UtcNow)
                .Inc(u => u.Version, 1);

            if (!string.IsNullOrEmpty(request.Email))
                updateBuilder = updateBuilder.Set(u => u.Email, request.Email);

            if (!string.IsNullOrEmpty(request.FirstName))
                updateBuilder = updateBuilder.Set(u => u.FirstName, request.FirstName);

            if (!string.IsNullOrEmpty(request.LastName))
                updateBuilder = updateBuilder.Set(u => u.LastName, request.LastName);

            if (!string.IsNullOrEmpty(request.PhoneNumber))
                updateBuilder = updateBuilder.Set(u => u.PhoneNumber, request.PhoneNumber);

            // Apply update
            var updateResult = await _usersCollection.UpdateOneAsync(session, filter, updateBuilder);

            if (updateResult.ModifiedCount == 0)
            {
                throw new InvalidOperationException($"Failed to update user {userId}");
            }

            // Get updated user
            var updatedUser = await _usersCollection
                .Find(session, Builders<User>.Filter.Eq(u => u.Id, userId))
                .FirstOrDefaultAsync();

            _logger.LogDebug("User {UserId} updated in MongoDB", userId);

            // Create outbox messages
            var userUpdatedEvent = new UserUpdated
            {
                Id = Guid.Parse(updatedUser.Id),
                Email = updatedUser.Email,
                FirstName = updatedUser.FirstName,
                LastName = updatedUser.LastName,
                UpdatedAt = updatedUser.UpdatedAt.Value,
                PreviousEmail = originalEmail,
                PreviousFirstName = originalFirstName,
                PreviousLastName = originalLastName
            };

            await _messageOutbox.PublishAsync(userUpdatedEvent);

            // Send email change notification if email changed
            if (updatedUser.Email != originalEmail)
            {
                var emailChangedEvent = new EmailChanged
                {
                    UserId = Guid.Parse(updatedUser.Id),
                    NewEmail = updatedUser.Email,
                    PreviousEmail = originalEmail,
                    ChangedAt = updatedUser.UpdatedAt.Value
                };

                await _messageOutbox.PublishAsync(emailChangedEvent);
            }

            await session.CommitTransactionAsync();
            _logger.LogInformation("User {UserId} updated successfully", userId);

            return updatedUser;
        }
        catch (Exception ex)
        {
            await session.AbortTransactionAsync();
            _logger.LogError(ex, "Error updating user {UserId}, transaction aborted", userId);
            throw;
        }
    }

    public async Task SoftDeleteUserAsync(string userId, string reason = null)
    {
        using var session = await _database.Client.StartSessionAsync();
        session.StartTransaction();

        try
        {
            _logger.LogInformation("Soft deleting user {UserId}", userId);

            var filter = Builders<User>.Filter.And(
                Builders<User>.Filter.Eq(u => u.Id, userId),
                Builders<User>.Filter.Eq(u => u.IsActive, true)
            );

            var user = await _usersCollection
                .Find(session, filter)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                throw new NotFoundException($"User {userId} not found");
            }

            // Soft delete update
            var update = Builders<User>.Update
                .Set(u => u.IsActive, false)
                .Set(u => u.DeletedAt, DateTime.UtcNow)
                .Set(u => u.DeletionReason, reason)
                .Set(u => u.UpdatedAt, DateTime.UtcNow)
                .Inc(u => u.Version, 1);

            await _usersCollection.UpdateOneAsync(session, filter, update);
            _logger.LogDebug("User {UserId} soft deleted in MongoDB", userId);

            // Create outbox messages
            var userDeletedEvent = new UserDeleted
            {
                Id = Guid.Parse(user.Id),
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                DeletedAt = DateTime.UtcNow,
                Reason = reason,
                SoftDelete = true
            };

            await _messageOutbox.PublishAsync(userDeletedEvent);

            var dataCleanupEvent = new ScheduleUserDataCleanup
            {
                UserId = Guid.Parse(user.Id),
                Email = user.Email,
                ScheduledAt = DateTime.UtcNow.AddDays(30)
            };

            await _messageOutbox.PublishAsync(dataCleanupEvent);

            await session.CommitTransactionAsync();
            _logger.LogInformation("User {UserId} soft deleted successfully", userId);
        }
        catch (Exception ex)
        {
            await session.AbortTransactionAsync();
            _logger.LogError(ex, "Error soft deleting user {UserId}, transaction aborted", userId);
            throw;
        }
    }

    public async Task<IEnumerable<User>> GetActiveUsersAsync(int page = 1, int pageSize = 20)
    {
        try
        {
            var users = await _usersCollection
                .Find(u => u.IsActive)
                .SortBy(u => u.LastName)
                .ThenBy(u => u.FirstName)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();

            _logger.LogDebug("Retrieved {Count} active users (page {Page})", users.Count, page);
            return users;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active users");
            throw;
        }
    }

    public async Task<User> GetUserByEmailAsync(string email)
    {
        try
        {
            var user = await _usersCollection
                .Find(u => u.Email == email && u.IsActive)
                .FirstOrDefaultAsync();

            if (user != null)
            {
                _logger.LogDebug("Found user {UserId} with email {Email}", user.Id, email);
            }
            else
            {
                _logger.LogDebug("No active user found with email {Email}", email);
            }

            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user by email {Email}", email);
            throw;
        }
    }

    public async Task<long> GetUserCountAsync()
    {
        try
        {
            return await _usersCollection.CountDocumentsAsync(u => u.IsActive);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user count");
            throw;
        }
    }
}

// Order service with MongoDB
public class OrderService
{
    private readonly IMongoDatabase _database;
    private readonly IMessageOutbox _messageOutbox;
    private readonly IMongoCollection<Order> _ordersCollection;
    private readonly IMongoCollection<Product> _productsCollection;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IMongoDatabase database,
        IMessageOutbox messageOutbox,
        ILogger<OrderService> logger)
    {
        _database = database;
        _messageOutbox = messageOutbox;
        _ordersCollection = database.GetCollection<Order>("orders");
        _productsCollection = database.GetCollection<Product>("products");
        _logger = logger;
    }

    public async Task<Order> CreateOrderAsync(CreateOrderRequest request)
    {
        using var session = await _database.Client.StartSessionAsync();
        session.StartTransaction();

        try
        {
            _logger.LogInformation("Creating order for user {UserId}", request.UserId);

            // Validate products exist and are active
            var productIds = request.Items.Select(i => i.ProductId).ToList();
            var products = await _productsCollection
                .Find(session, p => productIds.Contains(p.Id) && p.IsActive)
                .ToListAsync();

            if (products.Count != productIds.Count)
            {
                var missingIds = productIds.Except(products.Select(p => p.Id));
                throw new NotFoundException($"Products not found: {string.Join(", ", missingIds)}");
            }

            // Create order document
            var order = new Order
            {
                Id = Guid.NewGuid().ToString(),
                UserId = request.UserId,
                Status = OrderStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                Items = new List<OrderItem>(),
                Version = 1
            };

            // Create order items
            foreach (var requestItem in request.Items)
            {
                var product = products.First(p => p.Id == requestItem.ProductId);

                var orderItem = new OrderItem
                {
                    Id = Guid.NewGuid().ToString(),
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Quantity = requestItem.Quantity,
                    UnitPrice = product.Price,
                    TotalPrice = requestItem.Quantity * product.Price
                };

                order.Items.Add(orderItem);
            }

            order.TotalAmount = order.Items.Sum(i => i.TotalPrice);

            // Insert order
            await _ordersCollection.InsertOneAsync(session, order);
            _logger.LogDebug("Order {OrderId} saved to MongoDB with {ItemCount} items", order.Id, order.Items.Count);

            // Create outbox messages
            var orderCreatedEvent = new OrderCreated
            {
                Id = Guid.Parse(order.Id),
                UserId = request.UserId,
                Items = order.Items.Select(i => new OrderItemDto
                {
                    ProductId = i.ProductId,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    TotalPrice = i.TotalPrice
                }).ToList(),
                TotalAmount = order.TotalAmount,
                CreatedAt = order.CreatedAt
            };

            await _messageOutbox.PublishAsync(orderCreatedEvent);

            var orderNotificationEvent = new SendOrderConfirmation
            {
                OrderId = Guid.Parse(order.Id),
                UserId = request.UserId,
                TotalAmount = order.TotalAmount
            };

            await _messageOutbox.PublishAsync(orderNotificationEvent);

            await session.CommitTransactionAsync();
            _logger.LogInformation("Order {OrderId} created successfully", order.Id);

            return order;
        }
        catch (Exception ex)
        {
            await session.AbortTransactionAsync();
            _logger.LogError(ex, "Error creating order for user {UserId}, transaction aborted", request.UserId);
            throw;
        }
    }

    public async Task<Order> GetOrderAsync(string orderId)
    {
        try
        {
            var order = await _ordersCollection
                .Find(o => o.Id == orderId)
                .FirstOrDefaultAsync();

            if (order != null)
            {
                _logger.LogDebug("Retrieved order {OrderId} with {ItemCount} items", orderId, order.Items?.Count ?? 0);
            }
            else
            {
                _logger.LogDebug("Order {OrderId} not found", orderId);
            }

            return order;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving order {OrderId}", orderId);
            throw;
        }
    }

    public async Task<IEnumerable<Order>> GetUserOrdersAsync(string userId, int page = 1, int pageSize = 20)
    {
        try
        {
            var orders = await _ordersCollection
                .Find(o => o.UserId == userId)
                .SortByDescending(o => o.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();

            _logger.LogDebug("Retrieved {Count} orders for user {UserId} (page {Page})", orders.Count, userId, page);
            return orders;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving orders for user {UserId}", userId);
            throw;
        }
    }

    public async Task<OrderAnalytics> GetOrderAnalyticsAsync(DateTime fromDate, DateTime toDate)
    {
        try
        {
            var pipeline = new[]
            {
                new BsonDocument("$match", new BsonDocument
                {
                    ["createdAt"] = new BsonDocument
                    {
                        ["$gte"] = fromDate,
                        ["$lte"] = toDate
                    }
                }),
                new BsonDocument("$group", new BsonDocument
                {
                    ["_id"] = BsonNull.Value,
                    ["totalOrders"] = new BsonDocument("$sum", 1),
                    ["totalAmount"] = new BsonDocument("$sum", "$totalAmount"),
                    ["averageAmount"] = new BsonDocument("$avg", "$totalAmount"),
                    ["maxAmount"] = new BsonDocument("$max", "$totalAmount"),
                    ["minAmount"] = new BsonDocument("$min", "$totalAmount")
                })
            };

            var results = await _ordersCollection
                .Aggregate<BsonDocument>(pipeline)
                .FirstOrDefaultAsync();

            if (results == null)
            {
                return new OrderAnalytics
                {
                    FromDate = fromDate,
                    ToDate = toDate,
                    TotalOrders = 0,
                    TotalAmount = 0,
                    AverageAmount = 0,
                    MaxAmount = 0,
                    MinAmount = 0
                };
            }

            return new OrderAnalytics
            {
                FromDate = fromDate,
                ToDate = toDate,
                TotalOrders = results["totalOrders"].AsInt64,
                TotalAmount = results["totalAmount"].AsDecimal,
                AverageAmount = results["averageAmount"].AsDecimal,
                MaxAmount = results["maxAmount"].AsDecimal,
                MinAmount = results["minAmount"].AsDecimal
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting order analytics from {FromDate} to {ToDate}", fromDate, toDate);
            throw;
        }
    }
}
```

### 3. MongoDB Outbox Repository

Custom repository implementation for MongoDB:

```csharp
// MongoDB outbox repository
public class MongoOutboxRepository : IOutboxRepository
{
    private readonly IMongoCollection<MongoOutboxMessage> _collection;
    private readonly ILogger<MongoOutboxRepository> _logger;

    public MongoOutboxRepository(IMongoDatabase database, ILogger<MongoOutboxRepository> logger)
    {
        _collection = database.GetCollection<MongoOutboxMessage>("outbox_messages");
        _logger = logger;

        // Ensure indexes on startup
        EnsureIndexes();
    }

    public async Task<MongoOutboxMessage> GetByIdAsync(string id)
    {
        return await _collection
            .Find(m => m.Id == id)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<MongoOutboxMessage>> GetUnprocessedAsync(int batchSize = 100)
    {
        var filter = Builders<MongoOutboxMessage>.Filter.Or(
            Builders<MongoOutboxMessage>.Filter.Eq(m => m.Status, OutboxMessageStatus.Pending),
            Builders<MongoOutboxMessage>.Filter.And(
                Builders<MongoOutboxMessage>.Filter.Eq(m => m.Status, OutboxMessageStatus.Failed),
                Builders<MongoOutboxMessage>.Filter.Lt(m => m.Attempts, 5)
            )
        );

        // Add scheduled message filter
        var scheduledFilter = Builders<MongoOutboxMessage>.Filter.Or(
            Builders<MongoOutboxMessage>.Filter.Eq(m => m.ScheduledAt, BsonNull.Value),
            Builders<MongoOutboxMessage>.Filter.Lte(m => m.ScheduledAt, DateTime.UtcNow)
        );

        var combinedFilter = Builders<MongoOutboxMessage>.Filter.And(filter, scheduledFilter);

        return await _collection
            .Find(combinedFilter)
            .SortBy(m => m.Priority)
            .ThenBy(m => m.CreatedAt)
            .Limit(batchSize)
            .ToListAsync();
    }

    public async Task<long> GetPendingCountAsync()
    {
        return await _collection
            .CountDocumentsAsync(m => m.Status == OutboxMessageStatus.Pending);
    }

    public async Task<long> GetFailedCountAsync()
    {
        return await _collection
            .CountDocumentsAsync(m => m.Status == OutboxMessageStatus.Failed);
    }

    public async Task<long> GetDeadCountAsync()
    {
        return await _collection
            .CountDocumentsAsync(m => m.Status == OutboxMessageStatus.Dead);
    }

    public async Task CreateAsync(MongoOutboxMessage message)
    {
        message.CreatedAt = DateTime.UtcNow;
        message.Status = OutboxMessageStatus.Pending;
        message.Attempts = 0;

        // Set TTL if configured
        if (message.Ttl == null && TimeSpan.FromDays(30) > TimeSpan.Zero)
        {
            message.Ttl = DateTime.UtcNow.Add(TimeSpan.FromDays(30));
        }

        await _collection.InsertOneAsync(message);
        _logger.LogDebug("Outbox message {MessageId} created", message.Id);
    }

    public async Task UpdateAsync(MongoOutboxMessage message)
    {
        var filter = Builders<MongoOutboxMessage>.Filter.Eq(m => m.Id, message.Id);
        await _collection.ReplaceOneAsync(filter, message);
        _logger.LogDebug("Outbox message {MessageId} updated", message.Id);
    }

    public async Task<long> DeleteProcessedBeforeAsync(DateTime cutoffDate)
    {
        var filter = Builders<MongoOutboxMessage>.Filter.And(
            Builders<MongoOutboxMessage>.Filter.Eq(m => m.Status, OutboxMessageStatus.Processed),
            Builders<MongoOutboxMessage>.Filter.Lt(m => m.ProcessedAt, cutoffDate)
        );

        var result = await _collection.DeleteManyAsync(filter);

        _logger.LogDebug("Deleted {Count} processed outbox messages", result.DeletedCount);
        return result.DeletedCount;
    }

    public async Task<IEnumerable<MongoOutboxMessage>> GetMessagesByCorrelationIdAsync(string correlationId)
    {
        return await _collection
            .Find(m => m.CorrelationId == correlationId)
            .SortBy(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<MongoOutboxMessage>> GetMessagesByUserIdAsync(string userId, int limit = 100)
    {
        return await _collection
            .Find(m => m.UserId == userId)
            .SortByDescending(m => m.CreatedAt)
            .Limit(limit)
            .ToListAsync();
    }

    public async Task<IEnumerable<MongoOutboxMessage>> GetFailedMessagesAsync(int limit = 100)
    {
        return await _collection
            .Find(m => m.Status == OutboxMessageStatus.Failed)
            .SortBy(m => m.Attempts)
            .ThenBy(m => m.CreatedAt)
            .Limit(limit)
            .ToListAsync();
    }

    public async Task<BulkWriteResult<MongoOutboxMessage>> BulkUpdateStatusAsync(
        IEnumerable<string> messageIds,
        OutboxMessageStatus status)
    {
        var updates = messageIds.Select(id =>
        {
            var filter = Builders<MongoOutboxMessage>.Filter.Eq(m => m.Id, id);
            var update = Builders<MongoOutboxMessage>.Update
                .Set(m => m.Status, status)
                .Set(m => m.ProcessedAt, DateTime.UtcNow);

            return new UpdateOneModel<MongoOutboxMessage>(filter, update);
        });

        var result = await _collection.BulkWriteAsync(updates);
        _logger.LogDebug("Bulk updated {Count} outbox message statuses", result.ModifiedCount);

        return result;
    }

    public async Task<MessageStatistics> GetStatisticsAsync()
    {
        var pipeline = new[]
        {
            new BsonDocument("$group", new BsonDocument
            {
                ["_id"] = "$status",
                ["count"] = new BsonDocument("$sum", 1)
            })
        };

        var results = await _collection
            .Aggregate<BsonDocument>(pipeline)
            .ToListAsync();

        var statistics = new MessageStatistics();

        foreach (var result in results)
        {
            var status = result["_id"].AsString;
            var count = result["count"].AsInt64;

            switch (status)
            {
                case nameof(OutboxMessageStatus.Pending):
                    statistics.PendingCount = count;
                    break;
                case nameof(OutboxMessageStatus.Processing):
                    statistics.ProcessingCount = count;
                    break;
                case nameof(OutboxMessageStatus.Processed):
                    statistics.ProcessedCount = count;
                    break;
                case nameof(OutboxMessageStatus.Failed):
                    statistics.FailedCount = count;
                    break;
                case nameof(OutboxMessageStatus.Dead):
                    statistics.DeadCount = count;
                    break;
            }
        }

        return statistics;
    }

    private void EnsureIndexes()
    {
        try
        {
            var indexes = new[]
            {
                new CreateIndexModel<MongoOutboxMessage>(
                    Builders<MongoOutboxMessage>.IndexKeys.Ascending(m => m.Status)),

                new CreateIndexModel<MongoOutboxMessage>(
                    Builders<MongoOutboxMessage>.IndexKeys.Ascending(m => m.CreatedAt)),

                new CreateIndexModel<MongoOutboxMessage>(
                    Builders<MongoOutboxMessage>.IndexKeys.Combine(
                        Builders<MongoOutboxMessage>.IndexKeys.Ascending(m => m.Status),
                        Builders<MongoOutboxMessage>.IndexKeys.Ascending(m => m.CreatedAt))),

                new CreateIndexModel<MongoOutboxMessage>(
                    Builders<MongoOutboxMessage>.IndexKeys.Combine(
                        Builders<MongoOutboxMessage>.IndexKeys.Ascending(m => m.Status),
                        Builders<MongoOutboxMessage>.IndexKeys.Ascending(m => m.Attempts))),

                new CreateIndexModel<MongoOutboxMessage>(
                    Builders<MongoOutboxMessage>.IndexKeys.Ascending(m => m.CorrelationId)),

                new CreateIndexModel<MongoOutboxMessage>(
                    Builders<MongoOutboxMessage>.IndexKeys.Ascending(m => m.UserId)),

                new CreateIndexModel<MongoOutboxMessage>(
                    Builders<MongoOutboxMessage>.IndexKeys.Ascending(m => m.TenantId)),

                new CreateIndexModel<MongoOutboxMessage>(
                    Builders<MongoOutboxMessage>.IndexKeys.Combine(
                        Builders<MongoOutboxMessage>.IndexKeys.Ascending(m => m.Priority),
                        Builders<MongoOutboxMessage>.IndexKeys.Ascending(m => m.CreatedAt))),

                new CreateIndexModel<MongoOutboxMessage>(
                    Builders<MongoOutboxMessage>.IndexKeys.Ascending(m => m.ScheduledAt)),

                // TTL index for automatic cleanup
                new CreateIndexModel<MongoOutboxMessage>(
                    Builders<MongoOutboxMessage>.IndexKeys.Ascending(m => m.Ttl),
                    new CreateIndexOptions { ExpireAfter = TimeSpan.Zero })
            };

            _collection.Indexes.CreateMany(indexes);
            _logger.LogDebug("MongoDB outbox indexes created successfully");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create some MongoDB outbox indexes");
        }
    }
}

// Message statistics model
public class MessageStatistics
{
    public long PendingCount { get; set; }
    public long ProcessingCount { get; set; }
    public long ProcessedCount { get; set; }
    public long FailedCount { get; set; }
    public long DeadCount { get; set; }
    public long TotalCount => PendingCount + ProcessingCount + ProcessedCount + FailedCount + DeadCount;
}
```

### 4. Advanced MongoDB Features

Leverage MongoDB's advanced capabilities:

```csharp
// Sharding and partitioning service
public class MongoOutboxShardingService
{
    private readonly IMongoDatabase _database;
    private readonly ILogger<MongoOutboxShardingService> _logger;

    public MongoOutboxShardingService(IMongoDatabase database, ILogger<MongoOutboxShardingService> logger)
    {
        _database = database;
        _logger = logger;
    }

    public async Task EnableShardingAsync(string collectionName, string shardKey)
    {
        try
        {
            // Enable sharding on database
            var enableShardingCommand = new BsonDocument
            {
                { "enableSharding", _database.DatabaseNamespace.DatabaseName }
            };

            await _database.RunCommandAsync<BsonDocument>(enableShardingCommand);

            // Shard collection
            var shardCollectionCommand = new BsonDocument
            {
                { "shardCollection", $"{_database.DatabaseNamespace.DatabaseName}.{collectionName}" },
                { "key", new BsonDocument(shardKey, 1) }
            };

            await _database.RunCommandAsync<BsonDocument>(shardCollectionCommand);

            _logger.LogInformation("Enabled sharding for collection {Collection} with key {ShardKey}",
                collectionName, shardKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enabling sharding for collection {Collection}", collectionName);
            throw;
        }
    }

    public async Task CreateZoneAsync(string zoneName, string minKey, string maxKey)
    {
        try
        {
            var addShardToZoneCommand = new BsonDocument
            {
                { "addShardToZone", "shard01" },
                { "zone", zoneName }
            };

            await _database.RunCommandAsync<BsonDocument>(addShardToZoneCommand);

            var updateZoneKeyRangeCommand = new BsonDocument
            {
                { "updateZoneKeyRange", $"{_database.DatabaseNamespace.DatabaseName}.outbox_messages" },
                { "min", new BsonDocument("_id", minKey) },
                { "max", new BsonDocument("_id", maxKey) },
                { "zone", zoneName }
            };

            await _database.RunCommandAsync<BsonDocument>(updateZoneKeyRangeCommand);

            _logger.LogInformation("Created zone {Zone} with range {Min}-{Max}", zoneName, minKey, maxKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating zone {Zone}", zoneName);
            throw;
        }
    }
}

// GridFS large message storage
public class GridFSMessageStorageService
{
    private readonly IGridFSBucket _gridFSBucket;
    private readonly ILogger<GridFSMessageStorageService> _logger;

    public GridFSMessageStorageService(IMongoDatabase database, ILogger<GridFSMessageStorageService> logger)
    {
        _gridFSBucket = new GridFSBucket(database, new GridFSBucketOptions
        {
            BucketName = "outbox_messages",
            ChunkSizeBytes = 255 * 1024, // 255KB chunks
            WriteConcern = WriteConcern.WMajority
        });
        _logger = logger;
    }

    public async Task<string> StoreLargeMessageAsync(object message, Dictionary<string, object> metadata = null)
    {
        try
        {
            var json = JsonSerializer.Serialize(message, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });

            var bytes = Encoding.UTF8.GetBytes(json);

            var options = new GridFSUploadOptions
            {
                Metadata = metadata != null ? new BsonDocument(metadata) : null
            };

            var fileId = await _gridFSBucket.UploadFromBytesAsync(
                $"message_{Guid.NewGuid()}",
                bytes,
                options);

            _logger.LogDebug("Stored large message {FileId} with size {Size} bytes", fileId, bytes.Length);
            return fileId.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing large message to GridFS");
            throw;
        }
    }

    public async Task<T> RetrieveLargeMessageAsync<T>(string fileId) where T : class
    {
        try
        {
            var objectId = ObjectId.Parse(fileId);
            var bytes = await _gridFSBucket.DownloadAsBytesAsync(objectId);
            var json = Encoding.UTF8.GetString(bytes);

            var message = JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            });

            _logger.LogDebug("Retrieved large message {FileId} with size {Size} bytes", fileId, bytes.Length);
            return message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving large message {FileId} from GridFS", fileId);
            throw;
        }
    }

    public async Task DeleteLargeMessageAsync(string fileId)
    {
        try
        {
            var objectId = ObjectId.Parse(fileId);
            await _gridFSBucket.DeleteAsync(objectId);

            _logger.LogDebug("Deleted large message {FileId} from GridFS", fileId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting large message {FileId} from GridFS", fileId);
            throw;
        }
    }
}

// Change streams for real-time processing
public class MongoOutboxChangeStreamService : BackgroundService
{
    private readonly IMongoCollection<MongoOutboxMessage> _collection;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MongoOutboxChangeStreamService> _logger;

    public MongoOutboxChangeStreamService(
        IMongoDatabase database,
        IServiceProvider serviceProvider,
        ILogger<MongoOutboxChangeStreamService> logger)
    {
        _collection = database.GetCollection<MongoOutboxMessage>("outbox_messages");
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MongoDB change stream processor started");

        // Watch for new pending messages
        var pipeline = new[]
        {
            new BsonDocument("$match", new BsonDocument
            {
                ["operationType"] = "insert",
                ["fullDocument.status"] = nameof(OutboxMessageStatus.Pending)
            })
        };

        var options = new ChangeStreamOptions
        {
            FullDocument = ChangeStreamFullDocumentOption.UpdateLookup
        };

        try
        {
            using var cursor = await _collection.WatchAsync(pipeline, options, stoppingToken);

            await cursor.ForEachAsync(async change =>
            {
                try
                {
                    var message = change.FullDocument;
                    _logger.LogDebug("New outbox message detected: {MessageId}", message.Id);

                    // Trigger immediate processing for high-priority messages
                    if (message.Priority > 0)
                    {
                        using var scope = _serviceProvider.CreateScope();
                        var processor = scope.ServiceProvider.GetRequiredService<IOutboxProcessor>();
                        await processor.ProcessMessageAsync(message);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing change stream event");
                }
            }, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("MongoDB change stream processor cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in MongoDB change stream processor");
        }
    }
}
```

## Configuration Options

### MongoDB Outbox Options

```csharp
public class MongoOutboxOptions
{
    public string CollectionName { get; set; } = "outbox_messages";
    public string DatabaseName { get; set; }
    public bool EnableSharding { get; set; } = false;
    public string ShardKey { get; set; } = "_id";
    public int MaxDocumentSize { get; set; } = 16777216; // 16MB
    public bool EnableTransactions { get; set; } = true;
    public TransactionOptions TransactionOptions { get; set; }
    public bool EnableChangeStreams { get; set; } = false;
    public bool EnableGridFS { get; set; } = false;
    public int GridFSThreshold { get; set; } = 1048576; // 1MB
}
```

## API Reference

### Extension Methods

```csharp
public static class ConveyExtensions
{
    public static IConveyBuilder AddMongoOutbox(this IConveyBuilder builder);
    public static IConveyBuilder AddMongoOutbox(this IConveyBuilder builder, Action<MongoOutboxOptions> configure);
}
```

## Best Practices

1. **Use transactions** - Leverage MongoDB transactions for consistency
2. **Index optimization** - Create appropriate indexes for query patterns
3. **Sharding strategy** - Plan sharding for horizontal scalability
4. **Document design** - Design documents for optimal storage and queries
5. **GridFS for large messages** - Use GridFS for messages exceeding size limits
6. **Change streams** - Use change streams for real-time processing
7. **TTL indexes** - Use TTL indexes for automatic message cleanup
8. **Connection pooling** - Configure proper connection pool settings

## Troubleshooting

### Common Issues

1. **Transaction failures**
   - Ensure MongoDB supports transactions (replica set/sharded cluster)
   - Check transaction timeout settings
   - Monitor for write conflicts

2. **Performance problems**
   - Add missing indexes on query fields
   - Consider sharding for large collections
   - Monitor query execution statistics

3. **Memory usage**
   - Use projection to limit returned fields
   - Implement pagination for large result sets
   - Monitor working set size

4. **Replication lag**
   - Monitor replica set lag
   - Use appropriate read preferences
   - Consider read concern settings

