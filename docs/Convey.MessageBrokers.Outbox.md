# Convey.MessageBrokers.Outbox

Outbox pattern implementation for reliable message publishing ensuring transactional consistency between database operations and message broker publishing with at-least-once delivery guarantees.

## Installation

```bash
dotnet add package Convey.MessageBrokers.Outbox
```

## Overview

Convey.MessageBrokers.Outbox provides:
- **Outbox pattern** - Transactional message publishing with database consistency
- **At-least-once delivery** - Guaranteed message delivery with retry mechanisms
- **Persistent storage** - Outbox message storage and state management
- **Background processing** - Asynchronous message publishing workers
- **Message ordering** - Sequential message processing for ordered delivery
- **Cleanup automation** - Automatic cleanup of processed messages
- **Monitoring** - Message processing metrics and health monitoring
- **Multiple backends** - Support for various database providers

## Configuration

### Basic Outbox Setup

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddRabbitMq()
    .AddOutbox();

var app = builder.Build();
app.Run();
```

### Advanced Outbox Configuration

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddRabbitMq()
    .AddOutbox(outbox =>
    {
        outbox.Enabled = true;
        outbox.Type = "sequential"; // or "parallel"
        outbox.ExpireAfter = TimeSpan.FromDays(30);
        outbox.IntervalMilliseconds = 2000;
        outbox.InboxCheckIntervalMilliseconds = 2000;
        outbox.MaxRetries = 5;
        outbox.MaxUnprocessedAttempts = 3;
    });

var app = builder.Build();
app.Run();
```

### Database Integration

```csharp
// With Entity Framework
builder.Services.AddConvey()
    .AddRabbitMq()
    .AddOutbox()
    .AddEntityFrameworkOutbox<ApplicationDbContext>();

// With MongoDB
builder.Services.AddConvey()
    .AddRabbitMq()
    .AddOutbox()
    .AddMongoOutbox();
```

## Key Features

### 1. Transactional Message Publishing

Ensure message publishing consistency with database transactions:

```csharp
// User service with outbox pattern
public class UserService
{
    private readonly IUserRepository _userRepository;
    private readonly IMessageOutbox _messageOutbox;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UserService> _logger;

    public UserService(
        IUserRepository userRepository,
        IMessageOutbox messageOutbox,
        IUnitOfWork unitOfWork,
        ILogger<UserService> logger)
    {
        _userRepository = userRepository;
        _messageOutbox = messageOutbox;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<User> CreateUserAsync(CreateUserRequest request)
    {
        using var transaction = await _unitOfWork.BeginTransactionAsync();

        try
        {
            _logger.LogInformation("Creating user {Email}", request.Email);

            // Create user entity
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                DateOfBirth = request.DateOfBirth,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            // Save user to database
            await _userRepository.CreateAsync(user);
            _logger.LogDebug("User {UserId} saved to database", user.Id);

            // Create domain event
            var userCreatedEvent = new UserCreated
            {
                Id = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                CreatedAt = user.CreatedAt
            };

            // Add message to outbox (part of same transaction)
            await _messageOutbox.PublishAsync(userCreatedEvent);
            _logger.LogDebug("UserCreated event added to outbox for user {UserId}", user.Id);

            // Create welcome email message
            var welcomeEmailEvent = new SendWelcomeEmail
            {
                UserId = user.Id,
                RecipientEmail = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName
            };

            await _messageOutbox.PublishAsync(welcomeEmailEvent);
            _logger.LogDebug("SendWelcomeEmail event added to outbox for user {UserId}", user.Id);

            // Commit transaction - both user and outbox messages are saved atomically
            await transaction.CommitAsync();
            _logger.LogInformation("User {UserId} created successfully with outbox messages", user.Id);

            return user;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error creating user {Email}, transaction rolled back", request.Email);
            throw;
        }
    }

    public async Task UpdateUserAsync(Guid userId, UpdateUserRequest request)
    {
        using var transaction = await _unitOfWork.BeginTransactionAsync();

        try
        {
            _logger.LogInformation("Updating user {UserId}", userId);

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                throw new NotFoundException($"User {userId} not found");
            }

            // Store original values for event
            var originalEmail = user.Email;
            var originalFirstName = user.FirstName;
            var originalLastName = user.LastName;

            // Update user properties
            user.Email = request.Email ?? user.Email;
            user.FirstName = request.FirstName ?? user.FirstName;
            user.LastName = request.LastName ?? user.LastName;
            user.PhoneNumber = request.PhoneNumber ?? user.PhoneNumber;
            user.UpdatedAt = DateTime.UtcNow;

            // Save changes to database
            await _userRepository.UpdateAsync(user);
            _logger.LogDebug("User {UserId} updated in database", userId);

            // Create domain event
            var userUpdatedEvent = new UserUpdated
            {
                Id = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                UpdatedAt = user.UpdatedAt.Value,
                PreviousEmail = originalEmail,
                PreviousFirstName = originalFirstName,
                PreviousLastName = originalLastName
            };

            // Add message to outbox
            await _messageOutbox.PublishAsync(userUpdatedEvent);
            _logger.LogDebug("UserUpdated event added to outbox for user {UserId}", userId);

            // Send email notification if email changed
            if (user.Email != originalEmail)
            {
                var emailChangedEvent = new EmailChanged
                {
                    UserId = user.Id,
                    NewEmail = user.Email,
                    PreviousEmail = originalEmail,
                    ChangedAt = user.UpdatedAt.Value
                };

                await _messageOutbox.PublishAsync(emailChangedEvent);
                _logger.LogDebug("EmailChanged event added to outbox for user {UserId}", userId);
            }

            // Commit transaction
            await transaction.CommitAsync();
            _logger.LogInformation("User {UserId} updated successfully", userId);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error updating user {UserId}, transaction rolled back", userId);
            throw;
        }
    }

    public async Task DeleteUserAsync(Guid userId, string reason = null)
    {
        using var transaction = await _unitOfWork.BeginTransactionAsync();

        try
        {
            _logger.LogInformation("Deleting user {UserId}", userId);

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                throw new NotFoundException($"User {userId} not found");
            }

            // Soft delete user
            user.IsActive = false;
            user.DeletedAt = DateTime.UtcNow;
            user.DeletionReason = reason;

            await _userRepository.UpdateAsync(user);
            _logger.LogDebug("User {UserId} soft deleted in database", userId);

            // Create domain event
            var userDeletedEvent = new UserDeleted
            {
                Id = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                DeletedAt = user.DeletedAt.Value,
                Reason = reason
            };

            // Add message to outbox
            await _messageOutbox.PublishAsync(userDeletedEvent);
            _logger.LogDebug("UserDeleted event added to outbox for user {UserId}", userId);

            // Schedule data cleanup
            var dataCleanupEvent = new ScheduleUserDataCleanup
            {
                UserId = user.Id,
                Email = user.Email,
                ScheduledAt = DateTime.UtcNow.AddDays(30) // Cleanup in 30 days
            };

            await _messageOutbox.PublishAsync(dataCleanupEvent);
            _logger.LogDebug("ScheduleUserDataCleanup event added to outbox for user {UserId}", userId);

            // Commit transaction
            await transaction.CommitAsync();
            _logger.LogInformation("User {UserId} deleted successfully", userId);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error deleting user {UserId}, transaction rolled back", userId);
            throw;
        }
    }
}

// Order service with outbox pattern
public class OrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IInventoryService _inventoryService;
    private readonly IMessageOutbox _messageOutbox;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IOrderRepository orderRepository,
        IInventoryService inventoryService,
        IMessageOutbox messageOutbox,
        IUnitOfWork unitOfWork,
        ILogger<OrderService> logger)
    {
        _orderRepository = orderRepository;
        _inventoryService = inventoryService;
        _messageOutbox = messageOutbox;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Order> CreateOrderAsync(CreateOrderRequest request)
    {
        using var transaction = await _unitOfWork.BeginTransactionAsync();

        try
        {
            _logger.LogInformation("Creating order for user {UserId}", request.UserId);

            // Validate inventory availability
            foreach (var item in request.Items)
            {
                var available = await _inventoryService.CheckAvailabilityAsync(item.ProductId, item.Quantity);
                if (!available)
                {
                    throw new InsufficientInventoryException($"Insufficient inventory for product {item.ProductId}");
                }
            }

            // Create order
            var order = new Order
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                Items = request.Items.Select(i => new OrderItem
                {
                    ProductId = i.ProductId,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    TotalPrice = i.Quantity * i.UnitPrice
                }).ToList(),
                Status = OrderStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            order.TotalAmount = order.Items.Sum(i => i.TotalPrice);

            // Save order to database
            await _orderRepository.CreateAsync(order);
            _logger.LogDebug("Order {OrderId} saved to database", order.Id);

            // Reserve inventory
            foreach (var item in order.Items)
            {
                await _inventoryService.ReserveAsync(item.ProductId, item.Quantity, order.Id);
            }

            // Create domain events
            var orderCreatedEvent = new OrderCreated
            {
                Id = order.Id,
                UserId = order.UserId,
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
            _logger.LogDebug("OrderCreated event added to outbox for order {OrderId}", order.Id);

            // Create inventory reservation events
            foreach (var item in order.Items)
            {
                var reservationEvent = new InventoryReserved
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    OrderId = order.Id,
                    ReservedAt = DateTime.UtcNow
                };

                await _messageOutbox.PublishAsync(reservationEvent);
            }

            // Create order notification event
            var notificationEvent = new SendOrderConfirmation
            {
                OrderId = order.Id,
                UserId = order.UserId,
                TotalAmount = order.TotalAmount
            };

            await _messageOutbox.PublishAsync(notificationEvent);
            _logger.LogDebug("SendOrderConfirmation event added to outbox for order {OrderId}", order.Id);

            // Commit transaction
            await transaction.CommitAsync();
            _logger.LogInformation("Order {OrderId} created successfully", order.Id);

            return order;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error creating order for user {UserId}, transaction rolled back", request.UserId);
            throw;
        }
    }

    public async Task UpdateOrderStatusAsync(Guid orderId, OrderStatus newStatus)
    {
        using var transaction = await _unitOfWork.BeginTransactionAsync();

        try
        {
            _logger.LogInformation("Updating order {OrderId} status to {Status}", orderId, newStatus);

            var order = await _orderRepository.GetByIdAsync(orderId);
            if (order == null)
            {
                throw new NotFoundException($"Order {orderId} not found");
            }

            var previousStatus = order.Status;
            order.Status = newStatus;
            order.UpdatedAt = DateTime.UtcNow;

            // Update order in database
            await _orderRepository.UpdateAsync(order);
            _logger.LogDebug("Order {OrderId} status updated in database", orderId);

            // Create status update event
            var statusUpdatedEvent = new OrderStatusUpdated
            {
                Id = order.Id,
                UserId = order.UserId,
                PreviousStatus = previousStatus,
                NewStatus = newStatus,
                UpdatedAt = order.UpdatedAt.Value
            };

            await _messageOutbox.PublishAsync(statusUpdatedEvent);
            _logger.LogDebug("OrderStatusUpdated event added to outbox for order {OrderId}", orderId);

            // Handle specific status changes
            switch (newStatus)
            {
                case OrderStatus.Confirmed:
                    await HandleOrderConfirmedAsync(order);
                    break;
                case OrderStatus.Shipped:
                    await HandleOrderShippedAsync(order);
                    break;
                case OrderStatus.Delivered:
                    await HandleOrderDeliveredAsync(order);
                    break;
                case OrderStatus.Cancelled:
                    await HandleOrderCancelledAsync(order);
                    break;
            }

            // Commit transaction
            await transaction.CommitAsync();
            _logger.LogInformation("Order {OrderId} status updated successfully to {Status}", orderId, newStatus);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error updating order {OrderId} status, transaction rolled back", orderId);
            throw;
        }
    }

    private async Task HandleOrderConfirmedAsync(Order order)
    {
        // Create payment processing event
        var paymentEvent = new ProcessPayment
        {
            OrderId = order.Id,
            UserId = order.UserId,
            Amount = order.TotalAmount,
            Currency = "USD"
        };

        await _messageOutbox.PublishAsync(paymentEvent);
        _logger.LogDebug("ProcessPayment event added to outbox for order {OrderId}", order.Id);
    }

    private async Task HandleOrderShippedAsync(Order order)
    {
        // Create shipping notification event
        var shippingEvent = new OrderShipped
        {
            OrderId = order.Id,
            UserId = order.UserId,
            ShippedAt = DateTime.UtcNow,
            TrackingNumber = GenerateTrackingNumber()
        };

        await _messageOutbox.PublishAsync(shippingEvent);
        _logger.LogDebug("OrderShipped event added to outbox for order {OrderId}", order.Id);

        // Create tracking notification
        var trackingEvent = new SendShippingNotification
        {
            OrderId = order.Id,
            UserId = order.UserId,
            TrackingNumber = shippingEvent.TrackingNumber
        };

        await _messageOutbox.PublishAsync(trackingEvent);
    }

    private async Task HandleOrderDeliveredAsync(Order order)
    {
        // Create delivery confirmation event
        var deliveredEvent = new OrderDelivered
        {
            OrderId = order.Id,
            UserId = order.UserId,
            DeliveredAt = DateTime.UtcNow
        };

        await _messageOutbox.PublishAsync(deliveredEvent);
        _logger.LogDebug("OrderDelivered event added to outbox for order {OrderId}", order.Id);

        // Release inventory reservations
        foreach (var item in order.Items)
        {
            var releaseEvent = new ReleaseInventoryReservation
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                OrderId = order.Id,
                ReleasedAt = DateTime.UtcNow
            };

            await _messageOutbox.PublishAsync(releaseEvent);
        }
    }

    private async Task HandleOrderCancelledAsync(Order order)
    {
        // Create cancellation event
        var cancelledEvent = new OrderCancelled
        {
            OrderId = order.Id,
            UserId = order.UserId,
            CancelledAt = DateTime.UtcNow,
            Reason = "Order cancelled by user"
        };

        await _messageOutbox.PublishAsync(cancelledEvent);
        _logger.LogDebug("OrderCancelled event added to outbox for order {OrderId}", order.Id);

        // Return inventory to available stock
        foreach (var item in order.Items)
        {
            var returnEvent = new ReturnInventory
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                OrderId = order.Id,
                ReturnedAt = DateTime.UtcNow
            };

            await _messageOutbox.PublishAsync(returnEvent);
        }

        // Process refund if payment was processed
        var refundEvent = new ProcessRefund
        {
            OrderId = order.Id,
            UserId = order.UserId,
            Amount = order.TotalAmount,
            Reason = "Order cancellation"
        };

        await _messageOutbox.PublishAsync(refundEvent);
    }

    private string GenerateTrackingNumber()
    {
        return $"TRK{DateTime.UtcNow:yyyyMMdd}{Random.Shared.Next(1000, 9999)}";
    }
}
```

### 2. Message Processing Worker

Background service for processing outbox messages:

```csharp
// Outbox message processor
public class OutboxProcessor : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxProcessor> _logger;
    private readonly OutboxOptions _options;

    public OutboxProcessor(IServiceProvider serviceProvider, IOptions<OutboxOptions> options, ILogger<OutboxProcessor> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox processor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxMessagesAsync(stoppingToken);
                await Task.Delay(_options.IntervalMilliseconds, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in outbox processor");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        _logger.LogInformation("Outbox processor stopped");
    }

    private async Task ProcessOutboxMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        var messageBroker = scope.ServiceProvider.GetRequiredService<IMessageBroker>();

        try
        {
            // Get unprocessed messages
            var messages = await outboxRepository.GetUnprocessedAsync(batchSize: 100);

            if (messages.Any())
            {
                _logger.LogDebug("Processing {Count} outbox messages", messages.Count());

                foreach (var message in messages)
                {
                    await ProcessMessageAsync(message, messageBroker, outboxRepository, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing outbox messages batch");
        }
    }

    private async Task ProcessMessageAsync(
        OutboxMessage message,
        IMessageBroker messageBroker,
        IOutboxRepository outboxRepository,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Processing outbox message {MessageId} of type {MessageType}",
                message.Id, message.Type);

            // Mark as processing
            message.ProcessedAt = DateTime.UtcNow;
            message.Attempts++;

            // Deserialize and publish message
            var messageType = Type.GetType(message.Type);
            if (messageType == null)
            {
                _logger.LogWarning("Unknown message type {MessageType} for message {MessageId}", message.Type, message.Id);
                message.Status = OutboxMessageStatus.Failed;
                message.Error = $"Unknown message type: {message.Type}";
                await outboxRepository.UpdateAsync(message);
                return;
            }

            var messageData = JsonSerializer.Deserialize(message.Data, messageType);

            // Publish to message broker
            await messageBroker.PublishAsync(messageData, cancellationToken);

            // Mark as processed
            message.Status = OutboxMessageStatus.Processed;
            message.ProcessedAt = DateTime.UtcNow;

            await outboxRepository.UpdateAsync(message);

            _logger.LogDebug("Outbox message {MessageId} processed successfully", message.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing outbox message {MessageId}", message.Id);

            message.Status = OutboxMessageStatus.Failed;
            message.Error = ex.Message;
            message.ProcessedAt = DateTime.UtcNow;

            // Check if max retries exceeded
            if (message.Attempts >= _options.MaxRetries)
            {
                _logger.LogError("Outbox message {MessageId} exceeded max retries ({MaxRetries})",
                    message.Id, _options.MaxRetries);
                message.Status = OutboxMessageStatus.Dead;
            }

            await outboxRepository.UpdateAsync(message);
        }
    }
}

// Outbox cleanup service
public class OutboxCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxCleanupService> _logger;
    private readonly OutboxOptions _options;

    public OutboxCleanupService(IServiceProvider serviceProvider, IOptions<OutboxOptions> options, ILogger<OutboxCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox cleanup service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupProcessedMessagesAsync();
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken); // Run every hour
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in outbox cleanup service");
                await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
            }
        }

        _logger.LogInformation("Outbox cleanup service stopped");
    }

    private async Task CleanupProcessedMessagesAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();

        try
        {
            var cutoffDate = DateTime.UtcNow.Subtract(_options.ExpireAfter);
            var deletedCount = await outboxRepository.DeleteProcessedBeforeAsync(cutoffDate);

            if (deletedCount > 0)
            {
                _logger.LogInformation("Cleaned up {Count} processed outbox messages older than {CutoffDate}",
                    deletedCount, cutoffDate);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up processed outbox messages");
        }
    }
}

// Health check for outbox
public class OutboxHealthCheck : IHealthCheck
{
    private readonly IOutboxRepository _outboxRepository;
    private readonly ILogger<OutboxHealthCheck> _logger;

    public OutboxHealthCheck(IOutboxRepository outboxRepository, ILogger<OutboxHealthCheck> logger)
    {
        _outboxRepository = outboxRepository;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Check for failed messages
            var failedCount = await _outboxRepository.GetFailedCountAsync();
            var deadCount = await _outboxRepository.GetDeadCountAsync();
            var pendingCount = await _outboxRepository.GetPendingCountAsync();

            var data = new Dictionary<string, object>
            {
                ["failedMessages"] = failedCount,
                ["deadMessages"] = deadCount,
                ["pendingMessages"] = pendingCount
            };

            if (deadCount > 100)
            {
                return HealthCheckResult.Unhealthy($"Too many dead messages: {deadCount}", data: data);
            }

            if (failedCount > 500)
            {
                return HealthCheckResult.Degraded($"High number of failed messages: {failedCount}", data: data);
            }

            if (pendingCount > 1000)
            {
                return HealthCheckResult.Degraded($"High number of pending messages: {pendingCount}", data: data);
            }

            return HealthCheckResult.Healthy("Outbox is healthy", data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Outbox health check failed");
            return HealthCheckResult.Unhealthy($"Outbox health check failed: {ex.Message}");
        }
    }
}

// Register background services
builder.Services.AddHostedService<OutboxProcessor>();
builder.Services.AddHostedService<OutboxCleanupService>();
builder.Services.AddHealthChecks()
    .AddCheck<OutboxHealthCheck>("outbox");
```

### 3. Outbox Repository Implementation

Repository implementation for outbox message storage:

```csharp
// Outbox message entity
public class OutboxMessage
{
    public Guid Id { get; set; }
    public string Type { get; set; }
    public string Data { get; set; }
    public string CorrelationId { get; set; }
    public string UserId { get; set; }
    public OutboxMessageStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public int Attempts { get; set; }
    public string Error { get; set; }
}

public enum OutboxMessageStatus
{
    Pending,
    Processing,
    Processed,
    Failed,
    Dead
}

// Repository interface
public interface IOutboxRepository
{
    Task<OutboxMessage> GetByIdAsync(Guid id);
    Task<IEnumerable<OutboxMessage>> GetUnprocessedAsync(int batchSize = 100);
    Task<int> GetPendingCountAsync();
    Task<int> GetFailedCountAsync();
    Task<int> GetDeadCountAsync();
    Task CreateAsync(OutboxMessage message);
    Task UpdateAsync(OutboxMessage message);
    Task<int> DeleteProcessedBeforeAsync(DateTime cutoffDate);
}

// Entity Framework implementation
public class EntityFrameworkOutboxRepository : IOutboxRepository
{
    private readonly DbContext _context;
    private readonly ILogger<EntityFrameworkOutboxRepository> _logger;

    public EntityFrameworkOutboxRepository(DbContext context, ILogger<EntityFrameworkOutboxRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<OutboxMessage> GetByIdAsync(Guid id)
    {
        return await _context.Set<OutboxMessage>()
            .FirstOrDefaultAsync(m => m.Id == id);
    }

    public async Task<IEnumerable<OutboxMessage>> GetUnprocessedAsync(int batchSize = 100)
    {
        return await _context.Set<OutboxMessage>()
            .Where(m => m.Status == OutboxMessageStatus.Pending ||
                       (m.Status == OutboxMessageStatus.Failed && m.Attempts < 5))
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .ToListAsync();
    }

    public async Task<int> GetPendingCountAsync()
    {
        return await _context.Set<OutboxMessage>()
            .CountAsync(m => m.Status == OutboxMessageStatus.Pending);
    }

    public async Task<int> GetFailedCountAsync()
    {
        return await _context.Set<OutboxMessage>()
            .CountAsync(m => m.Status == OutboxMessageStatus.Failed);
    }

    public async Task<int> GetDeadCountAsync()
    {
        return await _context.Set<OutboxMessage>()
            .CountAsync(m => m.Status == OutboxMessageStatus.Dead);
    }

    public async Task CreateAsync(OutboxMessage message)
    {
        _context.Set<OutboxMessage>().Add(message);
        await _context.SaveChangesAsync();
        _logger.LogDebug("Outbox message {MessageId} created", message.Id);
    }

    public async Task UpdateAsync(OutboxMessage message)
    {
        _context.Set<OutboxMessage>().Update(message);
        await _context.SaveChangesAsync();
        _logger.LogDebug("Outbox message {MessageId} updated", message.Id);
    }

    public async Task<int> DeleteProcessedBeforeAsync(DateTime cutoffDate)
    {
        var messages = await _context.Set<OutboxMessage>()
            .Where(m => m.Status == OutboxMessageStatus.Processed && m.ProcessedAt < cutoffDate)
            .ToListAsync();

        _context.Set<OutboxMessage>().RemoveRange(messages);
        await _context.SaveChangesAsync();

        _logger.LogDebug("Deleted {Count} processed outbox messages", messages.Count);
        return messages.Count;
    }
}

// MongoDB implementation
public class MongoOutboxRepository : IOutboxRepository
{
    private readonly IMongoCollection<OutboxMessage> _collection;
    private readonly ILogger<MongoOutboxRepository> _logger;

    public MongoOutboxRepository(IMongoDatabase database, ILogger<MongoOutboxRepository> logger)
    {
        _collection = database.GetCollection<OutboxMessage>("outbox_messages");
        _logger = logger;
    }

    public async Task<OutboxMessage> GetByIdAsync(Guid id)
    {
        return await _collection
            .Find(m => m.Id == id)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<OutboxMessage>> GetUnprocessedAsync(int batchSize = 100)
    {
        var filter = Builders<OutboxMessage>.Filter.Or(
            Builders<OutboxMessage>.Filter.Eq(m => m.Status, OutboxMessageStatus.Pending),
            Builders<OutboxMessage>.Filter.And(
                Builders<OutboxMessage>.Filter.Eq(m => m.Status, OutboxMessageStatus.Failed),
                Builders<OutboxMessage>.Filter.Lt(m => m.Attempts, 5)
            )
        );

        return await _collection
            .Find(filter)
            .SortBy(m => m.CreatedAt)
            .Limit(batchSize)
            .ToListAsync();
    }

    public async Task<int> GetPendingCountAsync()
    {
        return (int)await _collection
            .CountDocumentsAsync(m => m.Status == OutboxMessageStatus.Pending);
    }

    public async Task<int> GetFailedCountAsync()
    {
        return (int)await _collection
            .CountDocumentsAsync(m => m.Status == OutboxMessageStatus.Failed);
    }

    public async Task<int> GetDeadCountAsync()
    {
        return (int)await _collection
            .CountDocumentsAsync(m => m.Status == OutboxMessageStatus.Dead);
    }

    public async Task CreateAsync(OutboxMessage message)
    {
        await _collection.InsertOneAsync(message);
        _logger.LogDebug("Outbox message {MessageId} created", message.Id);
    }

    public async Task UpdateAsync(OutboxMessage message)
    {
        await _collection.ReplaceOneAsync(m => m.Id == message.Id, message);
        _logger.LogDebug("Outbox message {MessageId} updated", message.Id);
    }

    public async Task<int> DeleteProcessedBeforeAsync(DateTime cutoffDate)
    {
        var filter = Builders<OutboxMessage>.Filter.And(
            Builders<OutboxMessage>.Filter.Eq(m => m.Status, OutboxMessageStatus.Processed),
            Builders<OutboxMessage>.Filter.Lt(m => m.ProcessedAt, cutoffDate)
        );

        var result = await _collection.DeleteManyAsync(filter);

        _logger.LogDebug("Deleted {Count} processed outbox messages", result.DeletedCount);
        return (int)result.DeletedCount;
    }
}
```

## Configuration Options

### Outbox Options

```csharp
public class OutboxOptions
{
    public bool Enabled { get; set; } = true;
    public string Type { get; set; } = "sequential";
    public TimeSpan ExpireAfter { get; set; } = TimeSpan.FromDays(30);
    public int IntervalMilliseconds { get; set; } = 2000;
    public int InboxCheckIntervalMilliseconds { get; set; } = 2000;
    public int MaxRetries { get; set; } = 5;
    public int MaxUnprocessedAttempts { get; set; } = 3;
}
```

## API Reference

### Extension Methods

```csharp
public static class ConveyExtensions
{
    public static IConveyBuilder AddOutbox(this IConveyBuilder builder);
    public static IConveyBuilder AddOutbox(this IConveyBuilder builder, Action<OutboxOptions> configure);
    public static IConveyBuilder AddEntityFrameworkOutbox<TContext>(this IConveyBuilder builder) where TContext : DbContext;
    public static IConveyBuilder AddMongoOutbox(this IConveyBuilder builder);
}
```

## Best Practices

1. **Use transactions** - Always use database transactions when adding outbox messages
2. **Handle duplicates** - Implement idempotent message handlers to handle duplicate processing
3. **Monitor health** - Implement health checks to monitor outbox message processing
4. **Configure retention** - Set appropriate message retention periods
5. **Error handling** - Implement proper error handling and dead letter processing
6. **Performance tuning** - Adjust batch sizes and processing intervals based on load
7. **Cleanup management** - Regularly clean up processed messages to maintain performance
8. **Correlation tracking** - Use correlation IDs for distributed tracing

## Troubleshooting

### Common Issues

1. **Messages not processed**
   - Check background service registration and startup
   - Verify database connectivity and permissions
   - Check for exceptions in outbox processor logs

2. **High number of failed messages**
   - Review message serialization and deserialization
   - Check message broker connectivity
   - Verify message handler implementations

3. **Performance issues**
   - Adjust processing interval and batch size
   - Monitor database query performance
   - Consider message partitioning strategies

4. **Transaction failures**
   - Ensure proper transaction scope management
   - Check for deadlocks and connection issues
   - Verify outbox repository implementation
