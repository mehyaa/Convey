---
layout: default
title: Convey.WebApi.CQRS
parent: Foundation
---
# Convey.WebApi.CQRS

Web API CQRS integration providing seamless command and query handling through HTTP endpoints with automatic request binding, validation, and response formatting for CQRS-based applications.

## Installation

```bash
dotnet add package Convey.WebApi.CQRS
```

## Overview

Convey.WebApi.CQRS provides:
- **CQRS endpoint integration** - Direct command and query handling in HTTP endpoints
- **Automatic request binding** - Bind HTTP requests to CQRS commands and queries
- **Validation integration** - Automatic request validation with detailed error responses
- **Response formatting** - Consistent response formatting for commands and queries
- **Error handling** - Standardized error handling for CQRS operations
- **Authentication integration** - Seamless integration with authentication and authorization
- **Correlation ID support** - Request correlation tracking for distributed systems

## Configuration

### Basic Setup

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddWebApi()
    .AddWebApiCqrs() // Enables CQRS web API integration
    .AddCommandHandlers()
    .AddQueryHandlers()
    .AddInMemoryCommandDispatcher()
    .AddInMemoryQueryDispatcher();

var app = builder.Build();

app.Run();
```

### Advanced Configuration

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddWebApi()
    .AddWebApiCqrs()
    .AddJwt() // Authentication
    .AddCommandHandlers()
    .AddQueryHandlers()
    .AddInMemoryCommandDispatcher()
    .AddInMemoryQueryDispatcher()
    .AddFluentValidation(); // Validation

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.Run();
```

## Key Features

### 1. Command Endpoints

Handle commands through HTTP endpoints:

```csharp
// Command definitions
public class CreateUserCommand : ICommand
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Email { get; set; }
    public string Name { get; set; }
    public string Password { get; set; }
    public string Role { get; set; } = "User";
}

public class UpdateUserCommand : ICommand
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
}

public class DeleteUserCommand : ICommand
{
    public Guid Id { get; set; }
}

// Command handlers
public class CreateUserHandler : ICommandHandler<CreateUserCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEventDispatcher _eventDispatcher;

    public CreateUserHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IEventDispatcher eventDispatcher)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _eventDispatcher = eventDispatcher;
    }

    public async Task HandleAsync(CreateUserCommand command, CancellationToken cancellationToken = default)
    {
        // Check if user already exists
        var existingUser = await _userRepository.GetByEmailAsync(command.Email);
        if (existingUser != null)
        {
            throw new UserAlreadyExistsException($"User with email {command.Email} already exists");
        }

        // Create user
        var hashedPassword = _passwordHasher.Hash(command.Password);
        var user = new User(command.Id, command.Email, command.Name, hashedPassword, command.Role);

        await _userRepository.AddAsync(user);

        // Publish event
        var userCreatedEvent = new UserCreatedEvent(user.Id, user.Email, user.Name);
        await _eventDispatcher.PublishAsync(userCreatedEvent);
    }
}

// CQRS endpoints using fluent API
app.UseEndpoints(endpoints =>
{
    // Command endpoints
    endpoints.Post<CreateUserCommand>("/api/users", async (command, ctx) =>
    {
        var commandDispatcher = ctx.RequestServices.GetRequiredService<ICommandDispatcher>();
        await commandDispatcher.SendAsync(command);

        ctx.Response.StatusCode = 201;
        ctx.Response.Headers["Location"] = $"/api/users/{command.Id}";
        await ctx.Response.WriteAsJsonAsync(new { Id = command.Id });
    });

    endpoints.Put<UpdateUserCommand>("/api/users/{id:guid}", async (command, ctx) =>
    {
        var commandDispatcher = ctx.RequestServices.GetRequiredService<ICommandDispatcher>();
        await commandDispatcher.SendAsync(command);
        ctx.Response.StatusCode = 204;
    });

    endpoints.Delete<DeleteUserCommand>("/api/users/{id:guid}", async (command, ctx) =>
    {
        var commandDispatcher = ctx.RequestServices.GetRequiredService<ICommandDispatcher>();
        await commandDispatcher.SendAsync(command);
        ctx.Response.StatusCode = 204;
    });
});
```

### 2. Query Endpoints

Handle queries through HTTP endpoints:

```csharp
// Query definitions
public class GetUserQuery : IQuery<UserDto>
{
    public Guid Id { get; set; }
}

public class BrowseUsersQuery : PagedQueryBase, IQuery<PagedResult<UserDto>>
{
    public string Email { get; set; }
    public string Role { get; set; }
    public bool? IsActive { get; set; }
    public DateTime? CreatedFrom { get; set; }
    public DateTime? CreatedTo { get; set; }
}

public class SearchUsersQuery : IQuery<IEnumerable<UserDto>>
{
    public string SearchTerm { get; set; }
    public int MaxResults { get; set; } = 10;
}

// Query handlers
public class GetUserHandler : IQueryHandler<GetUserQuery, UserDto>
{
    private readonly IUserRepository _userRepository;
    private readonly IMapper _mapper;

    public GetUserHandler(IUserRepository userRepository, IMapper mapper)
    {
        _userRepository = userRepository;
        _mapper = mapper;
    }

    public async Task<UserDto> HandleAsync(GetUserQuery query, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(query.Id);
        if (user == null)
        {
            throw new UserNotFoundException($"User with ID {query.Id} not found");
        }

        return _mapper.Map<UserDto>(user);
    }
}

public class BrowseUsersHandler : IQueryHandler<BrowseUsersQuery, PagedResult<UserDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly IMapper _mapper;

    public BrowseUsersHandler(IUserRepository userRepository, IMapper mapper)
    {
        _userRepository = userRepository;
        _mapper = mapper;
    }

    public async Task<PagedResult<UserDto>> HandleAsync(BrowseUsersQuery query, CancellationToken cancellationToken = default)
    {
        var result = await _userRepository.BrowseAsync(query);
        return new PagedResult<UserDto>(
            _mapper.Map<IEnumerable<UserDto>>(result.Items),
            result.TotalItems,
            result.Page,
            result.PageSize);
    }
}

// Query endpoints
app.UseEndpoints(endpoints =>
{
    // Query endpoints
    endpoints.Get<GetUserQuery, UserDto>("/api/users/{id:guid}", async (query, ctx) =>
    {
        var queryDispatcher = ctx.RequestServices.GetRequiredService<IQueryDispatcher>();
        var user = await queryDispatcher.QueryAsync(query);
        await ctx.Response.WriteAsJsonAsync(user);
    });

    endpoints.Get<BrowseUsersQuery, PagedResult<UserDto>>("/api/users", async (query, ctx) =>
    {
        var queryDispatcher = ctx.RequestServices.GetRequiredService<IQueryDispatcher>();
        var result = await queryDispatcher.QueryAsync(query);
        await ctx.Response.WriteAsJsonAsync(result);
    });

    endpoints.Get<SearchUsersQuery, IEnumerable<UserDto>>("/api/users/search", async (query, ctx) =>
    {
        var queryDispatcher = ctx.RequestServices.GetRequiredService<IQueryDispatcher>();
        var users = await queryDispatcher.QueryAsync(query);
        await ctx.Response.WriteAsJsonAsync(users);
    });
});
```

### 3. CRUD Endpoints with CQRS

Implement full CRUD operations using CQRS patterns:

```csharp
// Complete CRUD endpoints for users
app.UseEndpoints(endpoints =>
{
    // Create user (Command)
    endpoints.Post<CreateUserCommand>("/api/users",
        auth: true,
        roles: "Admin",
        context: async (command, ctx) =>
        {
            var commandDispatcher = ctx.RequestServices.GetRequiredService<ICommandDispatcher>();
            await commandDispatcher.SendAsync(command);

            ctx.Response.StatusCode = 201;
            ctx.Response.Headers["Location"] = $"/api/users/{command.Id}";
            await ctx.Response.WriteAsJsonAsync(new { Id = command.Id, Message = "User created successfully" });
        });

    // Get user by ID (Query)
    endpoints.Get<GetUserQuery, UserDto>("/api/users/{id:guid}",
        auth: true,
        context: async (query, ctx) =>
        {
            var queryDispatcher = ctx.RequestServices.GetRequiredService<IQueryDispatcher>();
            var user = await queryDispatcher.QueryAsync(query);
            await ctx.Response.WriteAsJsonAsync(user);
        });

    // Browse users with filtering (Query)
    endpoints.Get<BrowseUsersQuery, PagedResult<UserDto>>("/api/users",
        auth: true,
        context: async (query, ctx) =>
        {
            var queryDispatcher = ctx.RequestServices.GetRequiredService<IQueryDispatcher>();
            var result = await queryDispatcher.QueryAsync(query);

            // Add pagination headers
            ctx.Response.Headers["X-Total-Count"] = result.TotalItems.ToString();
            ctx.Response.Headers["X-Page"] = result.Page.ToString();
            ctx.Response.Headers["X-Page-Size"] = result.PageSize.ToString();
            ctx.Response.Headers["X-Total-Pages"] = result.TotalPages.ToString();

            await ctx.Response.WriteAsJsonAsync(result);
        });

    // Update user (Command)
    endpoints.Put<UpdateUserCommand>("/api/users/{id:guid}",
        auth: true,
        context: async (command, ctx) =>
        {
            var commandDispatcher = ctx.RequestServices.GetRequiredService<ICommandDispatcher>();
            await commandDispatcher.SendAsync(command);
            ctx.Response.StatusCode = 204;
        });

    // Delete user (Command)
    endpoints.Delete<DeleteUserCommand>("/api/users/{id:guid}",
        auth: true,
        roles: "Admin",
        context: async (command, ctx) =>
        {
            var commandDispatcher = ctx.RequestServices.GetRequiredService<ICommandDispatcher>();
            await commandDispatcher.SendAsync(command);
            ctx.Response.StatusCode = 204;
        });

    // Change user password (Command)
    endpoints.Post<ChangePasswordCommand>("/api/users/{id:guid}/change-password",
        auth: true,
        context: async (command, ctx) =>
        {
            var commandDispatcher = ctx.RequestServices.GetRequiredService<ICommandDispatcher>();
            await commandDispatcher.SendAsync(command);
            ctx.Response.StatusCode = 200;
            await ctx.Response.WriteAsJsonAsync(new { Message = "Password changed successfully" });
        });

    // Activate/Deactivate user (Command)
    endpoints.Patch<ChangeUserStatusCommand>("/api/users/{id:guid}/status",
        auth: true,
        roles: "Admin",
        context: async (command, ctx) =>
        {
            var commandDispatcher = ctx.RequestServices.GetRequiredService<ICommandDispatcher>();
            await commandDispatcher.SendAsync(command);
            ctx.Response.StatusCode = 200;
            await ctx.Response.WriteAsJsonAsync(new { Message = "User status updated successfully" });
        });
});
```

### 4. Request Validation Integration

Integrate with FluentValidation for request validation:

```csharp
// Validation for commands
public class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Email must be valid")
            .MaximumLength(256).WithMessage("Email must not exceed 256 characters");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MinimumLength(2).WithMessage("Name must be at least 2 characters")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters")
            .Matches(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)")
            .WithMessage("Password must contain at least one lowercase letter, one uppercase letter, and one digit");

        RuleFor(x => x.Role)
            .Must(BeValidRole).WithMessage("Role must be one of: Admin, Manager, Employee, Customer");
    }

    private bool BeValidRole(string role)
    {
        var validRoles = new[] { "Admin", "Manager", "Employee", "Customer" };
        return validRoles.Contains(role);
    }
}

// Validation for queries
public class BrowseUsersQueryValidator : AbstractValidator<BrowseUsersQuery>
{
    public BrowseUsersQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThan(0).WithMessage("Page must be greater than 0");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100");

        RuleFor(x => x.Email)
            .EmailAddress().When(x => !string.IsNullOrEmpty(x.Email))
            .WithMessage("Email filter must be a valid email address");

        RuleFor(x => x.CreatedFrom)
            .LessThanOrEqualTo(x => x.CreatedTo)
            .When(x => x.CreatedFrom.HasValue && x.CreatedTo.HasValue)
            .WithMessage("CreatedFrom must be less than or equal to CreatedTo");
    }
}

// Validation middleware
public class ValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IServiceProvider _serviceProvider;

    public ValidationMiddleware(RequestDelegate next, IServiceProvider serviceProvider)
    {
        _next = next;
        _serviceProvider = serviceProvider;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException ex)
        {
            await HandleValidationExceptionAsync(context, ex);
        }
    }

    private async Task HandleValidationExceptionAsync(HttpContext context, ValidationException ex)
    {
        context.Response.StatusCode = 400;
        context.Response.ContentType = "application/json";

        var errors = ex.Errors.GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key.ToCamelCase(),
                g => g.Select(e => e.ErrorMessage).ToArray());

        var response = new
        {
            Title = "Validation Failed",
            Status = 400,
            Errors = errors,
            TraceId = context.TraceIdentifier
        };

        await context.Response.WriteAsJsonAsync(response);
    }
}

// Register validation middleware
app.UseMiddleware<ValidationMiddleware>();
```

### 5. Response Formatting and Error Handling

Implement consistent response formatting:

```csharp
// Response models
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T Data { get; set; }
    public string Message { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string TraceId { get; set; }
}

public class ErrorResponse
{
    public bool Success { get; set; } = false;
    public string Title { get; set; }
    public string Detail { get; set; }
    public int Status { get; set; }
    public Dictionary<string, string[]> Errors { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string TraceId { get; set; }
}

// Response helper extensions
public static class HttpContextExtensions
{
    public static async Task WriteSuccessResponseAsync<T>(this HttpContext context, T data, string message = null)
    {
        var response = new ApiResponse<T>
        {
            Success = true,
            Data = data,
            Message = message,
            TraceId = context.TraceIdentifier
        };

        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(response);
    }

    public static async Task WriteErrorResponseAsync(this HttpContext context, int statusCode, string title, string detail = null, Dictionary<string, string[]> errors = null)
    {
        var response = new ErrorResponse
        {
            Title = title,
            Detail = detail,
            Status = statusCode,
            Errors = errors,
            TraceId = context.TraceIdentifier
        };

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(response);
    }
}

// Enhanced endpoints with response formatting
app.UseEndpoints(endpoints =>
{
    endpoints.Post<CreateUserCommand>("/api/users", async (command, ctx) =>
    {
        var commandDispatcher = ctx.RequestServices.GetRequiredService<ICommandDispatcher>();
        await commandDispatcher.SendAsync(command);

        ctx.Response.StatusCode = 201;
        ctx.Response.Headers["Location"] = $"/api/users/{command.Id}";

        await ctx.WriteSuccessResponseAsync(
            new { Id = command.Id },
            "User created successfully");
    });

    endpoints.Get<GetUserQuery, UserDto>("/api/users/{id:guid}", async (query, ctx) =>
    {
        var queryDispatcher = ctx.RequestServices.GetRequiredService<IQueryDispatcher>();
        var user = await queryDispatcher.QueryAsync(query);

        await ctx.WriteSuccessResponseAsync(user);
    });

    endpoints.Get<BrowseUsersQuery, PagedResult<UserDto>>("/api/users", async (query, ctx) =>
    {
        var queryDispatcher = ctx.RequestServices.GetRequiredService<IQueryDispatcher>();
        var result = await queryDispatcher.QueryAsync(query);

        // Add pagination metadata
        var response = new
        {
            Items = result.Items,
            Pagination = new
            {
                Page = result.Page,
                PageSize = result.PageSize,
                TotalItems = result.TotalItems,
                TotalPages = result.TotalPages,
                HasPrevious = result.Page > 1,
                HasNext = result.Page < result.TotalPages
            }
        };

        await ctx.WriteSuccessResponseAsync(response);
    });
});

// Global error handling middleware
public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, title, detail) = exception switch
        {
            ValidationException => (400, "Validation Failed", "One or more validation errors occurred"),
            UnauthorizedAccessException => (401, "Unauthorized", "Authentication is required"),
            ForbiddenException => (403, "Forbidden", "You don't have permission to access this resource"),
            NotFoundException => (404, "Not Found", "The requested resource was not found"),
            ConflictException => (409, "Conflict", "The request conflicts with the current state"),
            BusinessRuleException businessEx => (422, "Business Rule Violation", businessEx.Message),
            _ => (500, "Internal Server Error", "An unexpected error occurred")
        };

        Dictionary<string, string[]> errors = null;
        if (exception is ValidationException validationEx)
        {
            errors = validationEx.Errors.GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key.ToCamelCase(),
                    g => g.Select(e => e.ErrorMessage).ToArray());
        }

        await context.WriteErrorResponseAsync(statusCode, title, detail, errors);
    }
}
```

## Advanced Features

### 1. Endpoint Conventions for CQRS

Create reusable endpoint conventions:

```csharp
public static class CqrsEndpointConventions
{
    public static void MapCqrsEndpoints<TEntity, TKey>(
        this IEndpointsBuilder endpoints,
        string basePath,
        bool requireAuth = true,
        string requiredRole = null)
    {
        var entityName = typeof(TEntity).Name.ToLower();

        // Create endpoint
        endpoints.Post($"/api/{basePath}",
            auth: requireAuth,
            roles: requiredRole,
            context: async (CreateCommand<TEntity> command, HttpContext ctx) =>
            {
                var commandDispatcher = ctx.RequestServices.GetRequiredService<ICommandDispatcher>();
                await commandDispatcher.SendAsync(command);

                ctx.Response.StatusCode = 201;
                await ctx.WriteSuccessResponseAsync(
                    new { Id = command.Id },
                    $"{entityName} created successfully");
            });

        // Get by ID endpoint
{% raw %}
        endpoints.Get($"/api/{basePath}/{{id}}",
{% endraw %}
            auth: requireAuth,
            context: async (GetQuery<TEntity, TKey> query, HttpContext ctx) =>
            {
                var queryDispatcher = ctx.RequestServices.GetRequiredService<IQueryDispatcher>();
                var result = await queryDispatcher.QueryAsync(query);
                await ctx.WriteSuccessResponseAsync(result);
            });

        // Browse endpoint
        endpoints.Get($"/api/{basePath}",
            auth: requireAuth,
            context: async (BrowseQuery<TEntity> query, HttpContext ctx) =>
            {
                var queryDispatcher = ctx.RequestServices.GetRequiredService<IQueryDispatcher>();
                var result = await queryDispatcher.QueryAsync(query);
                await ctx.WriteSuccessResponseAsync(result);
            });

        // Update endpoint
{% raw %}
        endpoints.Put($"/api/{basePath}/{{id}}",
{% endraw %}
            auth: requireAuth,
            context: async (UpdateCommand<TEntity> command, HttpContext ctx) =>
            {
                var commandDispatcher = ctx.RequestServices.GetRequiredService<ICommandDispatcher>();
                await commandDispatcher.SendAsync(command);

                ctx.Response.StatusCode = 200;
                await ctx.WriteSuccessResponseAsync(
                    null,
                    $"{entityName} updated successfully");
            });

        // Delete endpoint
{% raw %}
        endpoints.Delete($"/api/{basePath}/{{id}}",
{% endraw %}
            auth: requireAuth,
            roles: requiredRole,
            context: async (DeleteCommand<TEntity> command, HttpContext ctx) =>
            {
                var commandDispatcher = ctx.RequestServices.GetRequiredService<ICommandDispatcher>();
                await commandDispatcher.SendAsync(command);

                ctx.Response.StatusCode = 200;
                await ctx.WriteSuccessResponseAsync(
                    null,
                    $"{entityName} deleted successfully");
            });
    }
}

// Usage
app.UseEndpoints(endpoints =>
{
    endpoints.MapCqrsEndpoints<User, Guid>("users", requireAuth: true, requiredRole: "Admin");
    endpoints.MapCqrsEndpoints<Product, Guid>("products", requireAuth: true);
    endpoints.MapCqrsEndpoints<Order, Guid>("orders", requireAuth: true);
});
```

### 2. Async Query Processing

Implement asynchronous query processing for long-running operations:

```csharp
// Async query interface
public interface IAsyncQuery<TResult> : IQuery<TResult>
{
    string QueryId { get; }
}

// Async query result
public class AsyncQueryResult<T>
{
    public string QueryId { get; set; }
    public AsyncQueryStatus Status { get; set; }
    public T Result { get; set; }
    public string ErrorMessage { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public enum AsyncQueryStatus
{
    Pending,
    InProgress,
    Completed,
    Failed
}

// Async query processor
public interface IAsyncQueryProcessor
{
    Task<string> SubmitQueryAsync<TQuery, TResult>(TQuery query)
        where TQuery : class, IAsyncQuery<TResult>;

    Task<AsyncQueryResult<TResult>> GetQueryResultAsync<TResult>(string queryId);
}

// Long-running report endpoint
app.UseEndpoints(endpoints =>
{
    // Submit async query
    endpoints.Post<GenerateReportQuery>("/api/reports/async", async (query, ctx) =>
    {
        var asyncProcessor = ctx.RequestServices.GetRequiredService<IAsyncQueryProcessor>();
        var queryId = await asyncProcessor.SubmitQueryAsync<GenerateReportQuery, ReportData>(query);

        ctx.Response.StatusCode = 202;
        ctx.Response.Headers["Location"] = $"/api/reports/async/{queryId}";

        await ctx.WriteSuccessResponseAsync(
            new { QueryId = queryId },
            "Report generation started");
    });

    // Get async query result
    endpoints.Get<GetAsyncQueryResultQuery, AsyncQueryResult<ReportData>>("/api/reports/async/{queryId}",
        async (query, ctx) =>
        {
            var asyncProcessor = ctx.RequestServices.GetRequiredService<IAsyncQueryProcessor>();
            var result = await asyncProcessor.GetQueryResultAsync<ReportData>(query.QueryId);

            if (result.Status == AsyncQueryStatus.InProgress)
            {
                ctx.Response.StatusCode = 202; // Still processing
            }

            await ctx.WriteSuccessResponseAsync(result);
        });
});
```

## Configuration Options

### CQRS Web API Settings

```csharp
public class CqrsWebApiOptions
{
    public bool EnableValidation { get; set; } = true;
    public bool EnableResponseFormatting { get; set; } = true;
    public bool EnableErrorHandling { get; set; } = true;
    public string DefaultPageSize { get; set; } = "20";
    public string MaxPageSize { get; set; } = "100";
}
```

## API Reference

### Extension Methods

```csharp
public static class ConveyExtensions
{
    public static IConveyBuilder AddWebApiCqrs(this IConveyBuilder builder);
    public static IConveyBuilder AddFluentValidation(this IConveyBuilder builder, Assembly assembly = null);

    // Endpoint helpers
    public static IEndpointsBuilder Get<TQuery, TResult>(this IEndpointsBuilder endpoints, string path,
        Func<TQuery, HttpContext, Task> context = null, bool auth = false, string roles = null)
        where TQuery : class, IQuery<TResult>;

    public static IEndpointsBuilder Post<TCommand>(this IEndpointsBuilder endpoints, string path,
        Func<TCommand, HttpContext, Task> context = null, bool auth = false, string roles = null)
        where TCommand : class, ICommand;
}
```

## Best Practices

1. **Separate commands and queries** - Keep clear separation between read and write operations
2. **Use appropriate HTTP verbs** - POST for commands, GET for queries
3. **Implement proper validation** - Validate all incoming requests
4. **Handle errors consistently** - Use standardized error response formats
5. **Use correlation IDs** - Track requests across the system
6. **Implement proper authorization** - Secure endpoints based on business requirements
7. **Return appropriate status codes** - Use correct HTTP status codes for different scenarios
8. **Document your API** - Provide clear API documentation for consumers

## Troubleshooting

### Common Issues

1. **Request binding failures**
   - Check that request models match expected JSON structure
   - Verify route parameter names match model properties
   - Ensure proper JSON serialization settings

2. **Validation not working**
   - Verify FluentValidation is properly registered
   - Check that validators are discovered and registered
   - Ensure validation middleware is added to pipeline

3. **Command/Query handlers not found**
   - Verify handlers are registered in DI container
   - Check that handler interfaces are properly implemented
   - Ensure assembly scanning is configured correctly

4. **Authorization failures**
   - Check authentication middleware configuration
   - Verify JWT token validation settings
   - Ensure proper role/permission configuration
