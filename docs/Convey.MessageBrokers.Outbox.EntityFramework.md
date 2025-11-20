# Convey.MessageBrokers.Outbox.EntityFramework

Entity Framework Core integration for the Outbox pattern providing database-agnostic outbox message storage with full EF Core features including migrations, change tracking, and LINQ support.

## Installation

```bash
dotnet add package Convey.MessageBrokers.Outbox.EntityFramework
```

## Overview

Convey.MessageBrokers.Outbox.EntityFramework provides:
- **EF Core integration** - Native Entity Framework Core outbox implementation
- **Database agnostic** - Support for SQL Server, PostgreSQL, MySQL, SQLite, and more
- **Migration support** - Automatic database schema creation and updates
- **Transaction support** - Full EF Core transaction integration
- **Change tracking** - Efficient change detection and batch operations
- **LINQ queries** - Rich querying capabilities for outbox messages
- **Connection management** - Automatic database connection handling
- **Performance optimization** - Bulk operations and query optimization

## Configuration

### Basic EntityFramework Outbox Setup

```csharp
var builder = WebApplication.CreateBuilder(args);

// Configure Entity Framework DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddConvey()
    .AddRabbitMq()
    .AddOutbox()
    .AddEntityFrameworkOutbox<ApplicationDbContext>();

var app = builder.Build();
app.Run();
```

### Advanced Configuration

```csharp
var builder = WebApplication.CreateBuilder(args);

// Configure multiple database contexts
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddDbContext<OutboxDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("OutboxConnection")));

builder.Services.AddConvey()
    .AddRabbitMq()
    .AddOutbox(outbox =>
    {
        outbox.Enabled = true;
        outbox.Type = "sequential";
        outbox.ExpireAfter = TimeSpan.FromDays(7);
        outbox.IntervalMilliseconds = 1000;
        outbox.MaxRetries = 3;
    })
    .AddEntityFrameworkOutbox<OutboxDbContext>(ef =>
    {
        ef.TableName = "MessageOutbox";
        ef.SchemaName = "messaging";
        ef.EnableSoftDelete = true;
        ef.BatchSize = 50;
    });

var app = builder.Build();
app.Run();
```

### Multi-Tenant Configuration

```csharp
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddConvey()
    .AddRabbitMq()
    .AddOutbox()
    .AddEntityFrameworkOutbox<ApplicationDbContext>(ef =>
    {
        ef.TableName = "Outbox";
        ef.EnableMultiTenant = true;
        ef.TenantIdColumn = "TenantId";
        ef.PartitionStrategy = PartitionStrategy.ByTenant;
    });
```

## Key Features

### 1. DbContext Configuration

Define your DbContext with outbox message support:

```csharp
// Application DbContext with outbox integration
public class ApplicationDbContext : DbContext, IOutboxDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // Application entities
    public DbSet<User> Users { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<Product> Products { get; set; }

    // Outbox messages
    public DbSet<OutboxMessage> OutboxMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure application entities
        ConfigureUserEntity(modelBuilder);
        ConfigureOrderEntity(modelBuilder);
        ConfigureProductEntity(modelBuilder);

        // Configure outbox messages
        ConfigureOutboxEntity(modelBuilder);
    }

    private void ConfigureUserEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(100);
            entity.Property(e => e.FirstName).IsRequired().HasMaxLength(50);
            entity.Property(e => e.LastName).IsRequired().HasMaxLength(50);
            entity.Property(e => e.PhoneNumber).HasMaxLength(20);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt);
            entity.Property(e => e.DeletedAt);
            entity.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);

            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.IsActive);
        });
    }

    private void ConfigureOrderEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.TotalAmount).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OrderId).IsRequired();
            entity.Property(e => e.ProductId).IsRequired();
            entity.Property(e => e.Quantity).IsRequired();
            entity.Property(e => e.UnitPrice).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(e => e.TotalPrice).IsRequired().HasColumnType("decimal(18,2)");

            entity.HasOne<Order>()
                .WithMany(o => o.Items)
                .HasForeignKey(e => e.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private void ConfigureProductEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Price).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(e => e.StockQuantity).IsRequired();
            entity.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);

            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.IsActive);
        });
    }

    private void ConfigureOutboxEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Type).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Data).IsRequired();
            entity.Property(e => e.CorrelationId).HasMaxLength(100);
            entity.Property(e => e.UserId).HasMaxLength(100);
            entity.Property(e => e.TenantId).HasMaxLength(100);
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.ProcessedAt);
            entity.Property(e => e.Attempts).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.Error).HasMaxLength(2000);
            entity.Property(e => e.IsDeleted).IsRequired().HasDefaultValue(false);

            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.ProcessedAt);
            entity.HasIndex(e => e.CorrelationId);
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => new { e.Status, e.CreatedAt });
            entity.HasIndex(e => new { e.Status, e.Attempts });
        });
    }
}

// Separate outbox-only DbContext
public class OutboxDbContext : DbContext, IOutboxDbContext
{
    public OutboxDbContext(DbContextOptions<OutboxDbContext> options)
        : base(options)
    {
    }

    public DbSet<OutboxMessage> OutboxMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("OutboxMessages", "messaging");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Type).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Data).IsRequired();
            entity.Property(e => e.CorrelationId).HasMaxLength(100);
            entity.Property(e => e.UserId).HasMaxLength(100);
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.ProcessedAt);
            entity.Property(e => e.Attempts).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.Error).HasMaxLength(2000);

            // Indexes for performance
            entity.HasIndex(e => e.Status).HasDatabaseName("IX_OutboxMessages_Status");
            entity.HasIndex(e => e.CreatedAt).HasDatabaseName("IX_OutboxMessages_CreatedAt");
            entity.HasIndex(e => new { e.Status, e.CreatedAt }).HasDatabaseName("IX_OutboxMessages_Status_CreatedAt");
            entity.HasIndex(e => new { e.Status, e.Attempts }).HasDatabaseName("IX_OutboxMessages_Status_Attempts");
        });
    }
}

// Interface for outbox DbContext
public interface IOutboxDbContext
{
    DbSet<OutboxMessage> OutboxMessages { get; set; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    DatabaseFacade Database { get; }
}
```

### 2. Service Implementation with EF Core

Use Entity Framework outbox in your services:

```csharp
// User service with EF Core outbox
public class UserService
{
    private readonly ApplicationDbContext _context;
    private readonly IMessageOutbox _messageOutbox;
    private readonly ILogger<UserService> _logger;

    public UserService(
        ApplicationDbContext context,
        IMessageOutbox messageOutbox,
        ILogger<UserService> logger)
    {
        _context = context;
        _messageOutbox = messageOutbox;
        _logger = logger;
    }

    public async Task<User> CreateUserAsync(CreateUserRequest request)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            _logger.LogInformation("Creating user {Email}", request.Email);

            // Check if user exists
            var existingUser = await _context.Users
                .Where(u => u.Email == request.Email && u.IsActive)
                .FirstOrDefaultAsync();

            if (existingUser != null)
            {
                throw new InvalidOperationException($"User with email {request.Email} already exists");
            }

            // Create user entity
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                DateOfBirth = request.DateOfBirth,
                PhoneNumber = request.PhoneNumber,
                Role = request.Role,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            // Add user to context
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _logger.LogDebug("User {UserId} saved to database", user.Id);

            // Create and save outbox messages within the same transaction
            var userCreatedEvent = new UserCreated
            {
                Id = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Role = user.Role,
                CreatedAt = user.CreatedAt
            };

            await _messageOutbox.PublishAsync(userCreatedEvent);

            var welcomeEmailEvent = new SendWelcomeEmail
            {
                UserId = user.Id,
                RecipientEmail = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName
            };

            await _messageOutbox.PublishAsync(welcomeEmailEvent);

            // Commit transaction
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

    public async Task<User> UpdateUserAsync(Guid userId, UpdateUserRequest request)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            _logger.LogInformation("Updating user {UserId}", userId);

            var user = await _context.Users
                .Where(u => u.Id == userId && u.IsActive)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                throw new NotFoundException($"User {userId} not found");
            }

            // Store original values
            var originalEmail = user.Email;
            var originalFirstName = user.FirstName;
            var originalLastName = user.LastName;

            // Update properties
            user.Email = request.Email ?? user.Email;
            user.FirstName = request.FirstName ?? user.FirstName;
            user.LastName = request.LastName ?? user.LastName;
            user.PhoneNumber = request.PhoneNumber ?? user.PhoneNumber;
            user.UpdatedAt = DateTime.UtcNow;

            // Mark as modified
            _context.Entry(user).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            _logger.LogDebug("User {UserId} updated in database", userId);

            // Create outbox messages
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

            await _messageOutbox.PublishAsync(userUpdatedEvent);

            // Send email change notification if email changed
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
            }

            await transaction.CommitAsync();
            _logger.LogInformation("User {UserId} updated successfully", userId);

            return user;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error updating user {UserId}, transaction rolled back", userId);
            throw;
        }
    }

    public async Task SoftDeleteUserAsync(Guid userId, string reason = null)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            _logger.LogInformation("Soft deleting user {UserId}", userId);

            var user = await _context.Users
                .Where(u => u.Id == userId && u.IsActive)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                throw new NotFoundException($"User {userId} not found");
            }

            // Soft delete
            user.IsActive = false;
            user.DeletedAt = DateTime.UtcNow;
            user.DeletionReason = reason;

            _context.Entry(user).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            _logger.LogDebug("User {UserId} soft deleted", userId);

            // Create outbox messages
            var userDeletedEvent = new UserDeleted
            {
                Id = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                DeletedAt = user.DeletedAt.Value,
                Reason = reason,
                SoftDelete = true
            };

            await _messageOutbox.PublishAsync(userDeletedEvent);

            var dataCleanupEvent = new ScheduleUserDataCleanup
            {
                UserId = user.Id,
                Email = user.Email,
                ScheduledAt = DateTime.UtcNow.AddDays(30)
            };

            await _messageOutbox.PublishAsync(dataCleanupEvent);

            await transaction.CommitAsync();
            _logger.LogInformation("User {UserId} soft deleted successfully", userId);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error soft deleting user {UserId}, transaction rolled back", userId);
            throw;
        }
    }

    public async Task<IEnumerable<User>> GetActiveUsersAsync(int page = 1, int pageSize = 20)
    {
        try
        {
            var users = await _context.Users
                .Where(u => u.IsActive)
                .OrderBy(u => u.LastName)
                .ThenBy(u => u.FirstName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
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
            var user = await _context.Users
                .Where(u => u.Email == email && u.IsActive)
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
}

// Order service with EF Core and outbox
public class OrderService
{
    private readonly ApplicationDbContext _context;
    private readonly IMessageOutbox _messageOutbox;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        ApplicationDbContext context,
        IMessageOutbox messageOutbox,
        ILogger<OrderService> logger)
    {
        _context = context;
        _messageOutbox = messageOutbox;
        _logger = logger;
    }

    public async Task<Order> CreateOrderAsync(CreateOrderRequest request)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            _logger.LogInformation("Creating order for user {UserId}", request.UserId);

            // Validate user exists
            var userExists = await _context.Users
                .AnyAsync(u => u.Id == request.UserId && u.IsActive);

            if (!userExists)
            {
                throw new NotFoundException($"User {request.UserId} not found");
            }

            // Validate products exist and are active
            var productIds = request.Items.Select(i => i.ProductId).ToList();
            var products = await _context.Products
                .Where(p => productIds.Contains(p.Id) && p.IsActive)
                .ToListAsync();

            if (products.Count != productIds.Count)
            {
                var missingIds = productIds.Except(products.Select(p => p.Id));
                throw new NotFoundException($"Products not found: {string.Join(", ", missingIds)}");
            }

            // Create order
            var order = new Order
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                Status = OrderStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                Items = new List<OrderItem>()
            };

            // Create order items
            foreach (var requestItem in request.Items)
            {
                var product = products.First(p => p.Id == requestItem.ProductId);

                var orderItem = new OrderItem
                {
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    ProductId = product.Id,
                    Quantity = requestItem.Quantity,
                    UnitPrice = product.Price,
                    TotalPrice = requestItem.Quantity * product.Price
                };

                order.Items.Add(orderItem);
            }

            order.TotalAmount = order.Items.Sum(i => i.TotalPrice);

            // Add order to context
            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            _logger.LogDebug("Order {OrderId} saved to database with {ItemCount} items", order.Id, order.Items.Count);

            // Create outbox messages
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

            var orderNotificationEvent = new SendOrderConfirmation
            {
                OrderId = order.Id,
                UserId = order.UserId,
                TotalAmount = order.TotalAmount
            };

            await _messageOutbox.PublishAsync(orderNotificationEvent);

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

    public async Task<Order> GetOrderWithItemsAsync(Guid orderId)
    {
        try
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order != null)
            {
                _logger.LogDebug("Retrieved order {OrderId} with {ItemCount} items", orderId, order.Items.Count);
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

    public async Task<IEnumerable<Order>> GetUserOrdersAsync(Guid userId, int page = 1, int pageSize = 20)
    {
        try
        {
            var orders = await _context.Orders
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Include(o => o.Items)
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
}
```

### 3. Migration and Database Setup

Create and manage database migrations:

```csharp
// Create initial migration
// dotnet ef migrations add InitialCreate --context ApplicationDbContext

// Migration for outbox support
public partial class AddOutboxSupport : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "OutboxMessages",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Type = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                Data = table.Column<string>(type: "nvarchar(max)", nullable: false),
                CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                UserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                TenantId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                Status = table.Column<int>(type: "int", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                Attempts = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                Error = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OutboxMessages", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_OutboxMessages_Status",
            table: "OutboxMessages",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "IX_OutboxMessages_CreatedAt",
            table: "OutboxMessages",
            column: "CreatedAt");

        migrationBuilder.CreateIndex(
            name: "IX_OutboxMessages_Status_CreatedAt",
            table: "OutboxMessages",
            columns: new[] { "Status", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_OutboxMessages_Status_Attempts",
            table: "OutboxMessages",
            columns: new[] { "Status", "Attempts" });

        migrationBuilder.CreateIndex(
            name: "IX_OutboxMessages_CorrelationId",
            table: "OutboxMessages",
            column: "CorrelationId");

        migrationBuilder.CreateIndex(
            name: "IX_OutboxMessages_TenantId",
            table: "OutboxMessages",
            column: "TenantId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "OutboxMessages");
    }
}

// Database initialization service
public class DatabaseInitializationService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DatabaseInitializationService> _logger;

    public DatabaseInitializationService(IServiceProvider serviceProvider, ILogger<DatabaseInitializationService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        try
        {
            _logger.LogInformation("Initializing database...");

            // Create database if it doesn't exist
            await context.Database.EnsureCreatedAsync(cancellationToken);

            // Apply pending migrations
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync(cancellationToken);
            if (pendingMigrations.Any())
            {
                _logger.LogInformation("Applying {Count} pending migrations", pendingMigrations.Count());
                await context.Database.MigrateAsync(cancellationToken);
                _logger.LogInformation("Database migrations applied successfully");
            }
            else
            {
                _logger.LogInformation("Database is up to date");
            }

            // Seed initial data if needed
            await SeedInitialDataAsync(context, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing database");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SeedInitialDataAsync(ApplicationDbContext context, CancellationToken cancellationToken)
    {
        try
        {
            // Seed products if none exist
            if (!await context.Products.AnyAsync(cancellationToken))
            {
                var products = new[]
                {
                    new Product { Id = Guid.NewGuid(), Name = "Laptop", Description = "High-performance laptop", Price = 999.99m, StockQuantity = 50, IsActive = true },
                    new Product { Id = Guid.NewGuid(), Name = "Mouse", Description = "Wireless mouse", Price = 29.99m, StockQuantity = 100, IsActive = true },
                    new Product { Id = Guid.NewGuid(), Name = "Keyboard", Description = "Mechanical keyboard", Price = 79.99m, StockQuantity = 75, IsActive = true }
                };

                context.Products.AddRange(products);
                await context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Seeded {Count} initial products", products.Length);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding initial data");
        }
    }
}

// Register database initialization
builder.Services.AddHostedService<DatabaseInitializationService>();
```

### 4. Performance Optimization

Optimize Entity Framework outbox performance:

```csharp
// Optimized outbox repository
public class OptimizedEntityFrameworkOutboxRepository : IOutboxRepository
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<OptimizedEntityFrameworkOutboxRepository> _logger;

    public OptimizedEntityFrameworkOutboxRepository(ApplicationDbContext context, ILogger<OptimizedEntityFrameworkOutboxRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IEnumerable<OutboxMessage>> GetUnprocessedAsync(int batchSize = 100)
    {
        return await _context.OutboxMessages
            .Where(m => m.Status == OutboxMessageStatus.Pending ||
                       (m.Status == OutboxMessageStatus.Failed && m.Attempts < 5))
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .AsNoTracking() // Improve performance for read-only operations
            .ToListAsync();
    }

    public async Task<int> GetPendingCountAsync()
    {
        return await _context.OutboxMessages
            .Where(m => m.Status == OutboxMessageStatus.Pending)
            .CountAsync();
    }

    public async Task<int> GetFailedCountAsync()
    {
        return await _context.OutboxMessages
            .Where(m => m.Status == OutboxMessageStatus.Failed)
            .CountAsync();
    }

    public async Task<int> GetDeadCountAsync()
    {
        return await _context.OutboxMessages
            .Where(m => m.Status == OutboxMessageStatus.Dead)
            .CountAsync();
    }

    public async Task CreateAsync(OutboxMessage message)
    {
        _context.OutboxMessages.Add(message);
        await _context.SaveChangesAsync();
        _logger.LogDebug("Outbox message {MessageId} created", message.Id);
    }

    public async Task UpdateAsync(OutboxMessage message)
    {
        _context.Entry(message).State = EntityState.Modified;
        await _context.SaveChangesAsync();
        _logger.LogDebug("Outbox message {MessageId} updated", message.Id);
    }

    public async Task UpdateBatchAsync(IEnumerable<OutboxMessage> messages)
    {
        foreach (var message in messages)
        {
            _context.Entry(message).State = EntityState.Modified;
        }

        var updatedCount = await _context.SaveChangesAsync();
        _logger.LogDebug("Updated {Count} outbox messages in batch", updatedCount);
    }

    public async Task<int> DeleteProcessedBeforeAsync(DateTime cutoffDate)
    {
        // Use raw SQL for better performance on large deletes
        var deletedCount = await _context.Database.ExecuteSqlRawAsync(
            "DELETE FROM OutboxMessages WHERE Status = {0} AND ProcessedAt < {1}",
            (int)OutboxMessageStatus.Processed,
            cutoffDate);

        _logger.LogDebug("Deleted {Count} processed outbox messages", deletedCount);
        return deletedCount;
    }

    public async Task<IEnumerable<OutboxMessage>> GetMessagesForCleanupAsync(DateTime cutoffDate, int batchSize = 1000)
    {
        return await _context.OutboxMessages
            .Where(m => m.Status == OutboxMessageStatus.Processed && m.ProcessedAt < cutoffDate)
            .OrderBy(m => m.ProcessedAt)
            .Take(batchSize)
            .AsNoTracking()
            .ToListAsync();
    }
}

// Bulk operations service
public class BulkOutboxOperationsService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<BulkOutboxOperationsService> _logger;

    public BulkOutboxOperationsService(ApplicationDbContext context, ILogger<BulkOutboxOperationsService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task BulkInsertOutboxMessagesAsync(IEnumerable<OutboxMessage> messages)
    {
        try
        {
            _context.OutboxMessages.AddRange(messages);
            var insertedCount = await _context.SaveChangesAsync();

            _logger.LogDebug("Bulk inserted {Count} outbox messages", insertedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error bulk inserting outbox messages");
            throw;
        }
    }

    public async Task BulkUpdateOutboxStatusAsync(IEnumerable<Guid> messageIds, OutboxMessageStatus status)
    {
        try
        {
            var messageIdList = messageIds.ToList();

            // Use raw SQL for better performance
            var updatedCount = await _context.Database.ExecuteSqlRawAsync(
                "UPDATE OutboxMessages SET Status = {0}, ProcessedAt = {1} WHERE Id IN ({2})",
                (int)status,
                DateTime.UtcNow,
                string.Join(",", messageIdList.Select(id => $"'{id}'")));

            _logger.LogDebug("Bulk updated {Count} outbox message statuses to {Status}", updatedCount, status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error bulk updating outbox message statuses");
            throw;
        }
    }

    public async Task<int> BulkDeleteProcessedMessagesAsync(DateTime cutoffDate, int batchSize = 1000)
    {
        try
        {
            var totalDeleted = 0;
            int deletedInBatch;

            do
            {
                deletedInBatch = await _context.Database.ExecuteSqlRawAsync(
                    "DELETE TOP({0}) FROM OutboxMessages WHERE Status = {1} AND ProcessedAt < {2}",
                    batchSize,
                    (int)OutboxMessageStatus.Processed,
                    cutoffDate);

                totalDeleted += deletedInBatch;

                _logger.LogDebug("Deleted {Count} messages in batch, total: {Total}", deletedInBatch, totalDeleted);

                // Add delay between batches to avoid blocking the database
                if (deletedInBatch > 0)
                {
                    await Task.Delay(100);
                }

            } while (deletedInBatch > 0);

            _logger.LogInformation("Bulk deleted {Total} processed outbox messages", totalDeleted);
            return totalDeleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error bulk deleting processed outbox messages");
            throw;
        }
    }
}
```

## Configuration Options

### EntityFramework Outbox Options

```csharp
public class EntityFrameworkOutboxOptions
{
    public string TableName { get; set; } = "OutboxMessages";
    public string SchemaName { get; set; } = null;
    public bool EnableSoftDelete { get; set; } = false;
    public bool EnableMultiTenant { get; set; } = false;
    public string TenantIdColumn { get; set; } = "TenantId";
    public PartitionStrategy PartitionStrategy { get; set; } = PartitionStrategy.None;
    public int BatchSize { get; set; } = 100;
    public int CommandTimeout { get; set; } = 30;
}

public enum PartitionStrategy
{
    None,
    ByTenant,
    ByDate,
    ByStatus
}
```

## API Reference

### Extension Methods

```csharp
public static class ConveyExtensions
{
    public static IConveyBuilder AddEntityFrameworkOutbox<TContext>(this IConveyBuilder builder) where TContext : DbContext, IOutboxDbContext;
    public static IConveyBuilder AddEntityFrameworkOutbox<TContext>(this IConveyBuilder builder, Action<EntityFrameworkOutboxOptions> configure) where TContext : DbContext, IOutboxDbContext;
}
```

## Best Practices

1. **Use transactions** - Always use database transactions for consistency
2. **Index optimization** - Create appropriate indexes for query performance
3. **Batch operations** - Use bulk operations for large data sets
4. **Connection management** - Use connection pooling and proper disposal
5. **Migration management** - Use EF migrations for schema changes
6. **Performance monitoring** - Monitor query performance and execution times
7. **Clean up regularly** - Implement automated cleanup of processed messages
8. **Use AsNoTracking** - Use for read-only operations to improve performance

## Troubleshooting

### Common Issues

1. **Migration failures**
   - Check database permissions and connectivity
   - Verify migration scripts and schema changes
   - Ensure proper EF Core configuration

2. **Performance problems**
   - Add missing indexes on outbox table
   - Use bulk operations for large datasets
   - Monitor query execution plans

3. **Transaction conflicts**
   - Check for deadlocks and lock timeouts
   - Implement proper retry logic
   - Use appropriate isolation levels

4. **Memory usage**
   - Use AsNoTracking for read operations
   - Implement proper disposal patterns
   - Avoid loading large datasets into memory
