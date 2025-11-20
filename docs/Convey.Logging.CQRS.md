# Convey.Logging.CQRS

CQRS-specific logging extensions providing structured logging for commands, queries, and events with automatic correlation tracking, performance monitoring, and distributed tracing integration.

## Installation

```bash
dotnet add package Convey.Logging.CQRS
```

## Overview

Convey.Logging.CQRS provides:
- **CQRS logging** - Specialized logging for commands, queries, and events
- **Correlation tracking** - Automatic correlation ID propagation across operations
- **Performance monitoring** - Execution time tracking and performance metrics
- **Structured logging** - Rich structured logs with contextual information
- **Error handling** - Comprehensive error logging and exception tracking
- **Audit trails** - Complete audit logs for business operations
- **Integration** - Seamless integration with existing logging infrastructure
- **Filtering** - Advanced filtering and log level configuration

## Configuration

### Basic CQRS Logging Setup

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddCommandHandlers()
    .AddEventHandlers()
    .AddQueryHandlers()
    .AddLogging()
    .AddCqrsLogging();

var app = builder.Build();
app.Run();
```

### Advanced Configuration

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddCommandHandlers()
    .AddEventHandlers()
    .AddQueryHandlers()
    .AddLogging()
    .AddCqrsLogging(cqrsLogging =>
    {
        cqrsLogging.LogCommands = true;
        cqrsLogging.LogQueries = true;
        cqrsLogging.LogEvents = true;
        cqrsLogging.LogPerformance = true;
        cqrsLogging.LogErrors = true;
        cqrsLogging.IncludePayload = true;
        cqrsLogging.MaxPayloadLength = 1000;
        cqrsLogging.SensitiveProperties = new[] { "password", "token", "secret" };
        cqrsLogging.PerformanceThreshold = TimeSpan.FromSeconds(1);
    });

var app = builder.Build();
app.Run();
```

### Serilog Integration

```csharp
builder.Host.UseSerilog((context, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithCorrelationId()
        .Enrich.WithMachineName()
        .Enrich.WithEnvironmentName()
        .WriteTo.Console(new JsonFormatter())
        .WriteTo.File(
            path: "logs/cqrs-.log",
            rollingInterval: RollingInterval.Day,
            formatter: new JsonFormatter())
        .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri("http://elasticsearch:9200"))
        {
            IndexFormat = "cqrs-logs-{0:yyyy.MM.dd}",
            AutoRegisterTemplate = true,
            AutoRegisterTemplateVersion = AutoRegisterTemplateVersion.ESv7
        });
});

builder.Services.AddConvey()
    .AddCommandHandlers()
    .AddEventHandlers()
    .AddQueryHandlers()
    .AddLogging()
    .AddCqrsLogging();
```

## Key Features

### 1. Command Logging

Automatic logging for command execution:

```csharp
// Command with logging attributes
[Log(IncludePayload = true, LogLevel = LogLevel.Information)]
public class CreateUser : ICommand
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [LogMask] // Masks sensitive data in logs
    public string Email { get; set; }

    public string FirstName { get; set; }
    public string LastName { get; set; }

    [LogIgnore] // Excludes from logging
    public string Password { get; set; }

    public DateTime? DateOfBirth { get; set; }
    public UserRole Role { get; set; }
}

[Log(IncludePayload = false, LogLevel = LogLevel.Warning)]
public class DeleteUser : ICommand
{
    public Guid Id { get; set; }

    [LogMask]
    public string Reason { get; set; }

    public bool SoftDelete { get; set; } = true;
}

public class UpdateUser : ICommand
{
    public Guid Id { get; set; }

    [LogMask]
    public string Email { get; set; }

    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string PhoneNumber { get; set; }
    public bool IsActive { get; set; }
}

// Command handler with automatic logging
public class CreateUserHandler : ICommandHandler<CreateUser>
{
    private readonly IUserRepository _userRepository;
    private readonly IEventBus _eventBus;
    private readonly ILogger<CreateUserHandler> _logger;

    public CreateUserHandler(
        IUserRepository userRepository,
        IEventBus eventBus,
        ILogger<CreateUserHandler> logger)
    {
        _userRepository = userRepository;
        _eventBus = eventBus;
        _logger = logger;
    }

    // Automatic logging via middleware - logs command execution, timing, and results
    public async Task HandleAsync(CreateUser command, CancellationToken cancellationToken = default)
    {
        // Business logic - logging is handled by middleware
        var existingUser = await _userRepository.GetByEmailAsync(command.Email);
        if (existingUser != null)
        {
            throw new UserAlreadyExistsException(command.Email);
        }

        var user = new User
        {
            Id = command.Id,
            Email = command.Email,
            FirstName = command.FirstName,
            LastName = command.LastName,
            DateOfBirth = command.DateOfBirth,
            Role = command.Role,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        await _userRepository.CreateAsync(user);

        // Publish domain event
        var userCreatedEvent = new UserCreated
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role,
            CreatedAt = user.CreatedAt
        };

        await _eventBus.PublishAsync(userCreatedEvent, cancellationToken);
    }
}

public class UpdateUserHandler : ICommandHandler<UpdateUser>
{
    private readonly IUserRepository _userRepository;
    private readonly IEventBus _eventBus;
    private readonly ILogger<UpdateUserHandler> _logger;

    public UpdateUserHandler(
        IUserRepository userRepository,
        IEventBus eventBus,
        ILogger<UpdateUserHandler> logger)
    {
        _userRepository = userRepository;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task HandleAsync(UpdateUser command, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(command.Id);
        if (user == null)
        {
            throw new UserNotFoundException(command.Id);
        }

        var originalEmail = user.Email;
        var originalFirstName = user.FirstName;
        var originalLastName = user.LastName;

        user.Email = command.Email ?? user.Email;
        user.FirstName = command.FirstName ?? user.FirstName;
        user.LastName = command.LastName ?? user.LastName;
        user.PhoneNumber = command.PhoneNumber ?? user.PhoneNumber;
        user.IsActive = command.IsActive;
        user.UpdatedAt = DateTime.UtcNow;

        await _userRepository.UpdateAsync(user);

        var userUpdatedEvent = new UserUpdated
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            IsActive = user.IsActive,
            UpdatedAt = user.UpdatedAt.Value,
            PreviousEmail = originalEmail,
            PreviousFirstName = originalFirstName,
            PreviousLastName = originalLastName
        };

        await _eventBus.PublishAsync(userUpdatedEvent, cancellationToken);
    }
}

public class DeleteUserHandler : ICommandHandler<DeleteUser>
{
    private readonly IUserRepository _userRepository;
    private readonly IEventBus _eventBus;
    private readonly ILogger<DeleteUserHandler> _logger;

    public DeleteUserHandler(
        IUserRepository userRepository,
        IEventBus eventBus,
        ILogger<DeleteUserHandler> logger)
    {
        _userRepository = userRepository;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task HandleAsync(DeleteUser command, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(command.Id);
        if (user == null)
        {
            throw new UserNotFoundException(command.Id);
        }

        if (command.SoftDelete)
        {
            user.IsActive = false;
            user.DeletedAt = DateTime.UtcNow;
            user.DeletionReason = command.Reason;
            await _userRepository.UpdateAsync(user);
        }
        else
        {
            await _userRepository.DeleteAsync(command.Id);
        }

        var userDeletedEvent = new UserDeleted
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            SoftDelete = command.SoftDelete,
            Reason = command.Reason,
            DeletedAt = DateTime.UtcNow
        };

        await _eventBus.PublishAsync(userDeletedEvent, cancellationToken);
    }
}
```

### 2. Query Logging

Automatic logging for query execution:

```csharp
// Query with logging configuration
[Log(IncludePayload = true, LogPerformance = true)]
public class GetUser : IQuery<UserDto>
{
    public Guid Id { get; set; }
}

[Log(IncludePayload = true, PerformanceThreshold = 500)] // Log if takes > 500ms
public class GetUsers : IQuery<PagedResult<UserDto>>
{
    public string SearchTerm { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public UserRole? Role { get; set; }
    public bool? IsActive { get; set; } = true;
    public DateTime? CreatedAfter { get; set; }
    public DateTime? CreatedBefore { get; set; }
}

[Log(LogLevel = LogLevel.Debug)]
public class GetUserStatistics : IQuery<UserStatisticsDto>
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public UserRole? Role { get; set; }
}

// Query handler with automatic logging
public class GetUserHandler : IQueryHandler<GetUser, UserDto>
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<GetUserHandler> _logger;

    public GetUserHandler(IUserRepository userRepository, ILogger<GetUserHandler> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<UserDto> HandleAsync(GetUser query, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(query.Id);
        if (user == null)
        {
            return null;
        }

        return new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }
}

public class GetUsersHandler : IQueryHandler<GetUsers, PagedResult<UserDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<GetUsersHandler> _logger;

    public GetUsersHandler(IUserRepository userRepository, ILogger<GetUsersHandler> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<PagedResult<UserDto>> HandleAsync(GetUsers query, CancellationToken cancellationToken = default)
    {
        var users = await _userRepository.GetUsersAsync(
            searchTerm: query.SearchTerm,
            page: query.Page,
            pageSize: query.PageSize,
            role: query.Role,
            isActive: query.IsActive,
            createdAfter: query.CreatedAfter,
            createdBefore: query.CreatedBefore);

        var totalCount = await _userRepository.GetUserCountAsync(
            searchTerm: query.SearchTerm,
            role: query.Role,
            isActive: query.IsActive,
            createdAfter: query.CreatedAfter,
            createdBefore: query.CreatedBefore);

        var userDtos = users.Select(u => new UserDto
        {
            Id = u.Id,
            Email = u.Email,
            FirstName = u.FirstName,
            LastName = u.LastName,
            Role = u.Role,
            IsActive = u.IsActive,
            CreatedAt = u.CreatedAt,
            UpdatedAt = u.UpdatedAt
        }).ToList();

        return new PagedResult<UserDto>
        {
            Items = userDtos,
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalPages = (int)Math.Ceiling((double)totalCount / query.PageSize)
        };
    }
}

public class GetUserStatisticsHandler : IQueryHandler<GetUserStatistics, UserStatisticsDto>
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<GetUserStatisticsHandler> _logger;

    public GetUserStatisticsHandler(IUserRepository userRepository, ILogger<GetUserStatisticsHandler> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<UserStatisticsDto> HandleAsync(GetUserStatistics query, CancellationToken cancellationToken = default)
    {
        var statistics = await _userRepository.GetStatisticsAsync(
            fromDate: query.FromDate,
            toDate: query.ToDate,
            role: query.Role);

        return new UserStatisticsDto
        {
            TotalUsers = statistics.TotalUsers,
            ActiveUsers = statistics.ActiveUsers,
            InactiveUsers = statistics.InactiveUsers,
            NewUsers = statistics.NewUsers,
            UsersByRole = statistics.UsersByRole,
            FromDate = query.FromDate,
            ToDate = query.ToDate
        };
    }
}
```

### 3. Event Logging

Automatic logging for event handling:

```csharp
// Event with logging configuration
[Log(IncludePayload = true, LogLevel = LogLevel.Information)]
public class UserCreated : IEvent
{
    public Guid Id { get; set; }

    [LogMask]
    public string Email { get; set; }

    public string FirstName { get; set; }
    public string LastName { get; set; }
    public UserRole Role { get; set; }
    public DateTime CreatedAt { get; set; }
}

[Log(IncludePayload = true)]
public class UserUpdated : IEvent
{
    public Guid Id { get; set; }

    [LogMask]
    public string Email { get; set; }

    public string FirstName { get; set; }
    public string LastName { get; set; }
    public bool IsActive { get; set; }
    public DateTime UpdatedAt { get; set; }

    [LogMask]
    public string PreviousEmail { get; set; }

    public string PreviousFirstName { get; set; }
    public string PreviousLastName { get; set; }
}

[Log(IncludePayload = false, LogLevel = LogLevel.Warning)]
public class UserDeleted : IEvent
{
    public Guid Id { get; set; }

    [LogMask]
    public string Email { get; set; }

    public string FirstName { get; set; }
    public string LastName { get; set; }
    public bool SoftDelete { get; set; }

    [LogMask]
    public string Reason { get; set; }

    public DateTime DeletedAt { get; set; }
}

// Event handlers with automatic logging
public class UserCreatedHandler : IEventHandler<UserCreated>
{
    private readonly IEmailService _emailService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<UserCreatedHandler> _logger;

    public UserCreatedHandler(
        IEmailService emailService,
        INotificationService notificationService,
        ILogger<UserCreatedHandler> logger)
    {
        _emailService = emailService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task HandleAsync(UserCreated @event, CancellationToken cancellationToken = default)
    {
        // Send welcome email
        await _emailService.SendWelcomeEmailAsync(new WelcomeEmailRequest
        {
            RecipientEmail = @event.Email,
            FirstName = @event.FirstName,
            LastName = @event.LastName,
            UserId = @event.Id
        });

        // Send admin notification for new users
        if (@event.Role != UserRole.Admin)
        {
            await _notificationService.SendAdminNotificationAsync(new AdminNotification
            {
                Type = NotificationType.NewUser,
                Message = $"New user registered: {@event.FirstName} {@event.LastName} ({@event.Email})",
                UserId = @event.Id,
                CreatedAt = @event.CreatedAt
            });
        }
    }
}

public class UserUpdatedHandler : IEventHandler<UserUpdated>
{
    private readonly IEmailService _emailService;
    private readonly ICacheService _cacheService;
    private readonly ILogger<UserUpdatedHandler> _logger;

    public UserUpdatedHandler(
        IEmailService emailService,
        ICacheService cacheService,
        ILogger<UserUpdatedHandler> logger)
    {
        _emailService = emailService;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task HandleAsync(UserUpdated @event, CancellationToken cancellationToken = default)
    {
        // Invalidate user cache
        await _cacheService.RemoveAsync($"user:{@event.Id}");
        await _cacheService.RemoveAsync($"user:email:{@event.Email}");

        // Send email notification if email changed
        if (@event.Email != @event.PreviousEmail)
        {
            await _emailService.SendEmailChangeNotificationAsync(new EmailChangeNotification
            {
                RecipientEmail = @event.Email,
                PreviousEmail = @event.PreviousEmail,
                FirstName = @event.FirstName,
                ChangedAt = @event.UpdatedAt
            });
        }
    }
}

public class UserDeletedHandler : IEventHandler<UserDeleted>
{
    private readonly IEmailService _emailService;
    private readonly ICacheService _cacheService;
    private readonly IDataRetentionService _dataRetentionService;
    private readonly ILogger<UserDeletedHandler> _logger;

    public UserDeletedHandler(
        IEmailService emailService,
        ICacheService cacheService,
        IDataRetentionService dataRetentionService,
        ILogger<UserDeletedHandler> logger)
    {
        _emailService = emailService;
        _cacheService = cacheService;
        _dataRetentionService = dataRetentionService;
        _logger = logger;
    }

    public async Task HandleAsync(UserDeleted @event, CancellationToken cancellationToken = default)
    {
        // Clean up user cache
        await _cacheService.RemoveAsync($"user:{@event.Id}");
        await _cacheService.RemoveAsync($"user:email:{@event.Email}");

        // Send account deletion confirmation email
        if (@event.SoftDelete)
        {
            await _emailService.SendAccountDeletionConfirmationAsync(new AccountDeletionEmail
            {
                RecipientEmail = @event.Email,
                FirstName = @event.FirstName,
                LastName = @event.LastName,
                DeletionReason = @event.Reason,
                DeletedAt = @event.DeletedAt
            });
        }

        // Schedule data retention cleanup
        if (!@event.SoftDelete)
        {
            await _dataRetentionService.ScheduleUserDataCleanupAsync(@event.Id);
        }
    }
}
```

### 4. CQRS Logging Middleware

Custom logging middleware for CQRS operations:

```csharp
// Command logging middleware
public class CommandLoggingMiddleware : ICommandHandlerMiddleware
{
    private readonly ICqrsLogger _cqrsLogger;
    private readonly ICorrelationIdProvider _correlationIdProvider;
    private readonly ILogger<CommandLoggingMiddleware> _logger;

    public CommandLoggingMiddleware(
        ICqrsLogger cqrsLogger,
        ICorrelationIdProvider correlationIdProvider,
        ILogger<CommandLoggingMiddleware> logger)
    {
        _cqrsLogger = cqrsLogger;
        _correlationIdProvider = correlationIdProvider;
        _logger = logger;
    }

    public async Task HandleAsync<T>(T command, Func<Task> next, CancellationToken cancellationToken = default) where T : class, ICommand
    {
        var correlationId = _correlationIdProvider.Get();
        var stopwatch = Stopwatch.StartNew();
        var commandName = typeof(T).Name;
        var commandId = Guid.NewGuid();

        try
        {
            await _cqrsLogger.LogCommandStartedAsync(new CommandLogContext
            {
                CommandId = commandId,
                CommandName = commandName,
                Command = command,
                CorrelationId = correlationId,
                UserId = GetUserId(command),
                StartedAt = DateTime.UtcNow
            });

            await next();

            stopwatch.Stop();

            await _cqrsLogger.LogCommandCompletedAsync(new CommandLogContext
            {
                CommandId = commandId,
                CommandName = commandName,
                Command = command,
                CorrelationId = correlationId,
                UserId = GetUserId(command),
                CompletedAt = DateTime.UtcNow,
                Duration = stopwatch.Elapsed,
                Success = true
            });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            await _cqrsLogger.LogCommandFailedAsync(new CommandLogContext
            {
                CommandId = commandId,
                CommandName = commandName,
                Command = command,
                CorrelationId = correlationId,
                UserId = GetUserId(command),
                CompletedAt = DateTime.UtcNow,
                Duration = stopwatch.Elapsed,
                Success = false,
                Exception = ex
            });

            throw;
        }
    }

    private string GetUserId<T>(T command) where T : class, ICommand
    {
        // Extract user ID from command if available
        var userIdProperty = typeof(T).GetProperty("UserId");
        return userIdProperty?.GetValue(command)?.ToString();
    }
}

// Query logging middleware
public class QueryLoggingMiddleware : IQueryHandlerMiddleware
{
    private readonly ICqrsLogger _cqrsLogger;
    private readonly ICorrelationIdProvider _correlationIdProvider;
    private readonly ILogger<QueryLoggingMiddleware> _logger;

    public QueryLoggingMiddleware(
        ICqrsLogger cqrsLogger,
        ICorrelationIdProvider correlationIdProvider,
        ILogger<QueryLoggingMiddleware> logger)
    {
        _cqrsLogger = cqrsLogger;
        _correlationIdProvider = correlationIdProvider;
        _logger = logger;
    }

    public async Task<TResult> HandleAsync<TQuery, TResult>(TQuery query, Func<Task<TResult>> next, CancellationToken cancellationToken = default) where TQuery : class, IQuery<TResult>
    {
        var correlationId = _correlationIdProvider.Get();
        var stopwatch = Stopwatch.StartNew();
        var queryName = typeof(TQuery).Name;
        var queryId = Guid.NewGuid();

        try
        {
            await _cqrsLogger.LogQueryStartedAsync(new QueryLogContext
            {
                QueryId = queryId,
                QueryName = queryName,
                Query = query,
                CorrelationId = correlationId,
                UserId = GetUserId(query),
                StartedAt = DateTime.UtcNow
            });

            var result = await next();

            stopwatch.Stop();

            await _cqrsLogger.LogQueryCompletedAsync(new QueryLogContext
            {
                QueryId = queryId,
                QueryName = queryName,
                Query = query,
                Result = result,
                CorrelationId = correlationId,
                UserId = GetUserId(query),
                CompletedAt = DateTime.UtcNow,
                Duration = stopwatch.Elapsed,
                Success = true
            });

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            await _cqrsLogger.LogQueryFailedAsync(new QueryLogContext
            {
                QueryId = queryId,
                QueryName = queryName,
                Query = query,
                CorrelationId = correlationId,
                UserId = GetUserId(query),
                CompletedAt = DateTime.UtcNow,
                Duration = stopwatch.Elapsed,
                Success = false,
                Exception = ex
            });

            throw;
        }
    }

    private string GetUserId<T>(T query) where T : class
    {
        var userIdProperty = typeof(T).GetProperty("UserId");
        return userIdProperty?.GetValue(query)?.ToString();
    }
}

// Event logging middleware
public class EventLoggingMiddleware : IEventHandlerMiddleware
{
    private readonly ICqrsLogger _cqrsLogger;
    private readonly ICorrelationIdProvider _correlationIdProvider;
    private readonly ILogger<EventLoggingMiddleware> _logger;

    public EventLoggingMiddleware(
        ICqrsLogger cqrsLogger,
        ICorrelationIdProvider correlationIdProvider,
        ILogger<EventLoggingMiddleware> logger)
    {
        _cqrsLogger = cqrsLogger;
        _correlationIdProvider = correlationIdProvider;
        _logger = logger;
    }

    public async Task HandleAsync<T>(T @event, Func<Task> next, CancellationToken cancellationToken = default) where T : class, IEvent
    {
        var correlationId = _correlationIdProvider.Get();
        var stopwatch = Stopwatch.StartNew();
        var eventName = typeof(T).Name;
        var eventId = Guid.NewGuid();

        try
        {
            await _cqrsLogger.LogEventStartedAsync(new EventLogContext
            {
                EventId = eventId,
                EventName = eventName,
                Event = @event,
                CorrelationId = correlationId,
                UserId = GetUserId(@event),
                StartedAt = DateTime.UtcNow
            });

            await next();

            stopwatch.Stop();

            await _cqrsLogger.LogEventCompletedAsync(new EventLogContext
            {
                EventId = eventId,
                EventName = eventName,
                Event = @event,
                CorrelationId = correlationId,
                UserId = GetUserId(@event),
                CompletedAt = DateTime.UtcNow,
                Duration = stopwatch.Elapsed,
                Success = true
            });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            await _cqrsLogger.LogEventFailedAsync(new EventLogContext
            {
                EventId = eventId,
                EventName = eventName,
                Event = @event,
                CorrelationId = correlationId,
                UserId = GetUserId(@event),
                CompletedAt = DateTime.UtcNow,
                Duration = stopwatch.Elapsed,
                Success = false,
                Exception = ex
            });

            throw;
        }
    }

    private string GetUserId<T>(T @event) where T : class, IEvent
    {
        var userIdProperty = typeof(T).GetProperty("UserId");
        return userIdProperty?.GetValue(@event)?.ToString();
    }
}

// CQRS logger implementation
public class CqrsLogger : ICqrsLogger
{
    private readonly ILogger<CqrsLogger> _logger;
    private readonly CqrsLoggingOptions _options;

    public CqrsLogger(ILogger<CqrsLogger> logger, IOptions<CqrsLoggingOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public async Task LogCommandStartedAsync(CommandLogContext context)
    {
        if (!_options.LogCommands) return;

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CommandId"] = context.CommandId,
            ["CommandName"] = context.CommandName,
            ["CorrelationId"] = context.CorrelationId,
            ["UserId"] = context.UserId
        });

        var message = "Command {CommandName} started with ID {CommandId}";
        var args = new object[] { context.CommandName, context.CommandId };

        if (_options.IncludePayload && context.Command != null)
        {
            var payload = SerializePayload(context.Command);
            message += " with payload {Payload}";
            args = args.Append(payload).ToArray();
        }

        _logger.LogInformation(message, args);
    }

    public async Task LogCommandCompletedAsync(CommandLogContext context)
    {
        if (!_options.LogCommands) return;

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CommandId"] = context.CommandId,
            ["CommandName"] = context.CommandName,
            ["CorrelationId"] = context.CorrelationId,
            ["UserId"] = context.UserId,
            ["Duration"] = context.Duration?.TotalMilliseconds
        });

        var message = "Command {CommandName} completed in {Duration}ms";

        if (_options.LogPerformance && context.Duration > _options.PerformanceThreshold)
        {
            _logger.LogWarning($"SLOW {message}", context.CommandName, context.Duration?.TotalMilliseconds);
        }
        else
        {
            _logger.LogInformation(message, context.CommandName, context.Duration?.TotalMilliseconds);
        }
    }

    public async Task LogCommandFailedAsync(CommandLogContext context)
    {
        if (!_options.LogErrors) return;

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CommandId"] = context.CommandId,
            ["CommandName"] = context.CommandName,
            ["CorrelationId"] = context.CorrelationId,
            ["UserId"] = context.UserId,
            ["Duration"] = context.Duration?.TotalMilliseconds
        });

        _logger.LogError(context.Exception,
            "Command {CommandName} failed after {Duration}ms",
            context.CommandName,
            context.Duration?.TotalMilliseconds);
    }

    public async Task LogQueryStartedAsync(QueryLogContext context)
    {
        if (!_options.LogQueries) return;

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["QueryId"] = context.QueryId,
            ["QueryName"] = context.QueryName,
            ["CorrelationId"] = context.CorrelationId,
            ["UserId"] = context.UserId
        });

        var message = "Query {QueryName} started with ID {QueryId}";
        var args = new object[] { context.QueryName, context.QueryId };

        if (_options.IncludePayload && context.Query != null)
        {
            var payload = SerializePayload(context.Query);
            message += " with payload {Payload}";
            args = args.Append(payload).ToArray();
        }

        _logger.LogInformation(message, args);
    }

    public async Task LogQueryCompletedAsync(QueryLogContext context)
    {
        if (!_options.LogQueries) return;

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["QueryId"] = context.QueryId,
            ["QueryName"] = context.QueryName,
            ["CorrelationId"] = context.CorrelationId,
            ["UserId"] = context.UserId,
            ["Duration"] = context.Duration?.TotalMilliseconds
        });

        var message = "Query {QueryName} completed in {Duration}ms";

        if (_options.LogPerformance && context.Duration > _options.PerformanceThreshold)
        {
            _logger.LogWarning($"SLOW {message}", context.QueryName, context.Duration?.TotalMilliseconds);
        }
        else
        {
            _logger.LogInformation(message, context.QueryName, context.Duration?.TotalMilliseconds);
        }
    }

    public async Task LogQueryFailedAsync(QueryLogContext context)
    {
        if (!_options.LogErrors) return;

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["QueryId"] = context.QueryId,
            ["QueryName"] = context.QueryName,
            ["CorrelationId"] = context.CorrelationId,
            ["UserId"] = context.UserId,
            ["Duration"] = context.Duration?.TotalMilliseconds
        });

        _logger.LogError(context.Exception,
            "Query {QueryName} failed after {Duration}ms",
            context.QueryName,
            context.Duration?.TotalMilliseconds);
    }

    public async Task LogEventStartedAsync(EventLogContext context)
    {
        if (!_options.LogEvents) return;

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["EventId"] = context.EventId,
            ["EventName"] = context.EventName,
            ["CorrelationId"] = context.CorrelationId,
            ["UserId"] = context.UserId
        });

        var message = "Event {EventName} handling started with ID {EventId}";
        var args = new object[] { context.EventName, context.EventId };

        if (_options.IncludePayload && context.Event != null)
        {
            var payload = SerializePayload(context.Event);
            message += " with payload {Payload}";
            args = args.Append(payload).ToArray();
        }

        _logger.LogInformation(message, args);
    }

    public async Task LogEventCompletedAsync(EventLogContext context)
    {
        if (!_options.LogEvents) return;

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["EventId"] = context.EventId,
            ["EventName"] = context.EventName,
            ["CorrelationId"] = context.CorrelationId,
            ["UserId"] = context.UserId,
            ["Duration"] = context.Duration?.TotalMilliseconds
        });

        _logger.LogInformation("Event {EventName} handled in {Duration}ms",
            context.EventName,
            context.Duration?.TotalMilliseconds);
    }

    public async Task LogEventFailedAsync(EventLogContext context)
    {
        if (!_options.LogErrors) return;

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["EventId"] = context.EventId,
            ["EventName"] = context.EventName,
            ["CorrelationId"] = context.CorrelationId,
            ["UserId"] = context.UserId,
            ["Duration"] = context.Duration?.TotalMilliseconds
        });

        _logger.LogError(context.Exception,
            "Event {EventName} handling failed after {Duration}ms",
            context.EventName,
            context.Duration?.TotalMilliseconds);
    }

    private string SerializePayload(object payload)
    {
        try
        {
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });

            // Mask sensitive properties
            foreach (var sensitiveProperty in _options.SensitiveProperties)
            {
                json = MaskSensitiveProperty(json, sensitiveProperty);
            }

            // Truncate if too long
            if (json.Length > _options.MaxPayloadLength)
            {
                json = json.Substring(0, _options.MaxPayloadLength) + "...";
            }

            return json;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to serialize payload");
            return "[Serialization Failed]";
        }
    }

    private string MaskSensitiveProperty(string json, string propertyName)
    {
        var pattern = $"\"{propertyName}\"\\s*:\\s*\"[^\"]*\"";
        return Regex.Replace(json, pattern, $"\"{propertyName}\":\"***\"", RegexOptions.IgnoreCase);
    }
}
```

## Configuration Options

### CQRS Logging Options

```csharp
public class CqrsLoggingOptions
{
    public bool LogCommands { get; set; } = true;
    public bool LogQueries { get; set; } = true;
    public bool LogEvents { get; set; } = true;
    public bool LogPerformance { get; set; } = true;
    public bool LogErrors { get; set; } = true;
    public bool IncludePayload { get; set; } = false;
    public int MaxPayloadLength { get; set; } = 1000;
    public string[] SensitiveProperties { get; set; } = new[] { "password", "token", "secret" };
    public TimeSpan PerformanceThreshold { get; set; } = TimeSpan.FromSeconds(1);
}
```

## API Reference

### Extension Methods

```csharp
public static class ConveyExtensions
{
    public static IConveyBuilder AddCqrsLogging(this IConveyBuilder builder);
    public static IConveyBuilder AddCqrsLogging(this IConveyBuilder builder, Action<CqrsLoggingOptions> configure);
}
```

### Logging Attributes

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
public class LogAttribute : Attribute
{
    public bool IncludePayload { get; set; } = true;
    public LogLevel LogLevel { get; set; } = LogLevel.Information;
    public bool LogPerformance { get; set; } = true;
    public int PerformanceThreshold { get; set; } = 1000; // milliseconds
}

[AttributeUsage(AttributeTargets.Property)]
public class LogMaskAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Property)]
public class LogIgnoreAttribute : Attribute { }
```

## Best Practices

1. **Use structured logging** - Leverage structured logging for better analysis
2. **Mask sensitive data** - Always mask sensitive information in logs
3. **Monitor performance** - Track execution times and identify slow operations
4. **Implement correlation tracking** - Use correlation IDs for distributed tracing
5. **Configure log levels** - Set appropriate log levels for different environments
6. **Use log aggregation** - Implement centralized log aggregation and analysis
7. **Monitor log volume** - Control log volume to avoid overwhelming systems
8. **Implement log retention** - Set appropriate log retention policies

## Troubleshooting

### Common Issues

1. **High log volume**
   - Adjust log levels and filtering
   - Reduce payload logging for high-frequency operations
   - Implement sampling for high-volume events

2. **Sensitive data exposure**
   - Review and update sensitive property lists
   - Implement proper masking and filtering
   - Audit log outputs regularly

3. **Performance impact**
   - Optimize logging serialization
   - Use asynchronous logging
   - Consider log buffering and batching

4. **Missing correlation IDs**
   - Ensure correlation ID middleware is properly configured
   - Verify correlation ID propagation across services
   - Check correlation ID generation and storage
