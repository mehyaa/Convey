---
layout: default
title: Convey.WebApi.Swagger
parent: HTTP & API
---
# Convey.WebApi.Swagger

Swagger/OpenAPI integration for Web API documentation, providing automatic API documentation generation, interactive testing interface, and comprehensive API specification for Convey-based applications.

## Installation

```bash
dotnet add package Convey.WebApi.Swagger
```

## Overview

Convey.WebApi.Swagger provides:
- **Swagger/OpenAPI 3.0 support** - Automatic API documentation generation
- **Interactive documentation** - Swagger UI for testing APIs
- **Custom documentation** - Rich API descriptions and examples
- **Authentication integration** - JWT and API key documentation
- **Model documentation** - Automatic schema generation
- **API versioning support** - Multiple API version documentation
- **Custom themes** - Customizable Swagger UI appearance
- **Export capabilities** - JSON/YAML specification export

## Configuration

### Basic Swagger Setup

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddWebApi()
    .AddSwagger();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.Run();
```

### Advanced Swagger Configuration

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddWebApi()
    .AddSwagger(swagger =>
    {
        swagger.Title = "Convey API";
        swagger.Description = "A comprehensive API built with Convey framework";
        swagger.Version = "v1.0";
        swagger.ContactName = "API Support";
        swagger.ContactEmail = "support@example.com";
        swagger.ContactUrl = "https://example.com/support";
        swagger.LicenseName = "MIT";
        swagger.LicenseUrl = "https://opensource.org/licenses/MIT";
        swagger.TermsOfService = "https://example.com/terms";

        swagger.IncludeXmlComments = true;
        swagger.XmlCommentsFilePath = "MyApi.xml";

        swagger.EnableAnnotations = true;
        swagger.EnableFilters = true;
        swagger.EnableOperationIds = true;

        swagger.AddSecurity = true;
        swagger.AddSecurityDefinitions = true;

        swagger.SerializeAsV2 = false; // Use OpenAPI 3.0

        swagger.Servers = new[]
        {
            new SwaggerServer { Url = "https://api.example.com", Description = "Production" },
            new SwaggerServer { Url = "https://staging.api.example.com", Description = "Staging" },
            new SwaggerServer { Url = "https://localhost:5001", Description = "Development" }
        };

        swagger.Tags = new[]
        {
            new SwaggerTag { Name = "Users", Description = "User management operations" },
            new SwaggerTag { Name = "Orders", Description = "Order processing operations" },
            new SwaggerTag { Name = "Products", Description = "Product catalog operations" }
        };
    });

var app = builder.Build();

if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
{
    app.UseSwagger(c =>
    {
        c.RouteTemplate = "api-docs/{documentName}/swagger.json";
        c.PreSerializeFilters.Add((swagger, httpReq) =>
        {
            swagger.Servers = new List<OpenApiServer>
            {
                new() { Url = $"{httpReq.Scheme}://{httpReq.Host.Value}" }
            };
        });
    });

    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/api-docs/v1/swagger.json", "Convey API v1");
        c.RoutePrefix = "docs";
        c.DocumentTitle = "Convey API Documentation";
        c.EnableDeepLinking();
        c.EnableFilter();
        c.EnableTryItOutByDefault();
        c.DisplayRequestDuration();
        c.DocExpansion(DocExpansion.None);
        c.DefaultModelsExpandDepth(-1);
        c.EnablePersistAuthorization();
    });
}

app.Run();
```

## Key Features

### 1. API Documentation with Attributes

Comprehensive API documentation using attributes:

```csharp
/// <summary>
/// User management operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[ProducesResponseType(typeof(ErrorResponse), 400)]
[ProducesResponseType(typeof(ErrorResponse), 401)]
[ProducesResponseType(typeof(ErrorResponse), 500)]
[SwaggerTag("Users", "User management and authentication operations")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    /// <summary>
    /// Creates a new user account
    /// </summary>
    /// <param name="request">User creation details</param>
    /// <returns>Created user information</returns>
    /// <remarks>
    /// Sample request:
    ///
    ///     POST /api/users
    ///     {
    ///         "email": "john.doe@example.com",
    ///         "name": "John Doe",
    ///         "password": "SecurePassword123!",
    ///         "role": "User"
    ///     }
    ///
    /// Password requirements:
    /// - Minimum 8 characters
    /// - At least one uppercase letter
    /// - At least one lowercase letter
    /// - At least one digit
    /// - At least one special character
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(typeof(UserResponse), 201)]
    [ProducesResponseType(typeof(ValidationErrorResponse), 400)]
    [ProducesResponseType(typeof(ErrorResponse), 409)]
    [SwaggerOperation(
        Summary = "Create user",
        Description = "Creates a new user account with the provided information",
        OperationId = "CreateUser",
        Tags = new[] { "Users" }
    )]
    [SwaggerResponse(201, "User created successfully", typeof(UserResponse))]
    [SwaggerResponse(400, "Invalid input data", typeof(ValidationErrorResponse))]
    [SwaggerResponse(409, "User already exists", typeof(ErrorResponse))]
    public async Task<ActionResult<UserResponse>> CreateUser([FromBody] CreateUserRequest request)
    {
        var user = await _userService.CreateUserAsync(request);
        var response = new UserResponse(user);

        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, response);
    }

    /// <summary>
    /// Retrieves a user by ID
    /// </summary>
    /// <param name="id">User identifier</param>
    /// <returns>User information</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(UserResponse), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    [SwaggerOperation(
        Summary = "Get user by ID",
        Description = "Retrieves detailed information for a specific user",
        OperationId = "GetUser"
    )]
    [SwaggerResponse(200, "User found", typeof(UserResponse))]
    [SwaggerResponse(404, "User not found", typeof(ErrorResponse))]
    public async Task<ActionResult<UserResponse>> GetUser([FromRoute] Guid id)
    {
        var user = await _userService.GetUserAsync(id);
        if (user == null)
        {
            return NotFound(new ErrorResponse("User not found"));
        }

        return Ok(new UserResponse(user));
    }

    /// <summary>
    /// Retrieves users with filtering and pagination
    /// </summary>
    /// <param name="query">Search and filter parameters</param>
    /// <returns>Paginated list of users</returns>
    /// <remarks>
    /// Supports filtering by:
    /// - Email (partial match)
    /// - Role (exact match)
    /// - Active status
    /// - Creation date range
    ///
    /// Results are paginated with default page size of 20.
    /// Maximum page size is 100.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<UserSummaryResponse>), 200)]
    [SwaggerOperation(
        Summary = "Browse users",
        Description = "Retrieves a paginated list of users with optional filtering",
        OperationId = "BrowseUsers"
    )]
    [SwaggerResponse(200, "Users retrieved successfully", typeof(PagedResponse<UserSummaryResponse>))]
    public async Task<ActionResult<PagedResponse<UserSummaryResponse>>> BrowseUsers([FromQuery] BrowseUsersQuery query)
    {
        var result = await _userService.BrowseUsersAsync(query);
        var response = new PagedResponse<UserSummaryResponse>(
            result.Items.Select(u => new UserSummaryResponse(u)),
            result.TotalItems,
            result.Page,
            result.PageSize
        );

        return Ok(response);
    }

    /// <summary>
    /// Updates an existing user
    /// </summary>
    /// <param name="id">User identifier</param>
    /// <param name="request">User update details</param>
    /// <returns>No content on success</returns>
    [HttpPut("{id:guid}")]
    [Authorize]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ValidationErrorResponse), 400)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    [SwaggerOperation(
        Summary = "Update user",
        Description = "Updates an existing user's information",
        OperationId = "UpdateUser"
    )]
    [SwaggerResponse(204, "User updated successfully")]
    [SwaggerResponse(400, "Invalid input data", typeof(ValidationErrorResponse))]
    [SwaggerResponse(404, "User not found", typeof(ErrorResponse))]
    public async Task<IActionResult> UpdateUser([FromRoute] Guid id, [FromBody] UpdateUserRequest request)
    {
        request.Id = id;
        await _userService.UpdateUserAsync(request);
        return NoContent();
    }

    /// <summary>
    /// Deletes a user account
    /// </summary>
    /// <param name="id">User identifier</param>
    /// <returns>No content on success</returns>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ErrorResponse), 403)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    [SwaggerOperation(
        Summary = "Delete user",
        Description = "Permanently deletes a user account (Admin only)",
        OperationId = "DeleteUser"
    )]
    [SwaggerResponse(204, "User deleted successfully")]
    [SwaggerResponse(403, "Insufficient permissions", typeof(ErrorResponse))]
    [SwaggerResponse(404, "User not found", typeof(ErrorResponse))]
    public async Task<IActionResult> DeleteUser([FromRoute] Guid id)
    {
        await _userService.DeleteUserAsync(id);
        return NoContent();
    }

    /// <summary>
    /// Changes user password
    /// </summary>
    /// <param name="id">User identifier</param>
    /// <param name="request">Password change details</param>
    /// <returns>Success message</returns>
    [HttpPost("{id:guid}/change-password")]
    [Authorize]
    [ProducesResponseType(typeof(SuccessResponse), 200)]
    [ProducesResponseType(typeof(ValidationErrorResponse), 400)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    [SwaggerOperation(
        Summary = "Change password",
        Description = "Changes the user's password after validating the current password",
        OperationId = "ChangePassword"
    )]
    [SwaggerResponse(200, "Password changed successfully", typeof(SuccessResponse))]
    [SwaggerResponse(400, "Invalid password data", typeof(ValidationErrorResponse))]
    [SwaggerResponse(404, "User not found", typeof(ErrorResponse))]
    public async Task<ActionResult<SuccessResponse>> ChangePassword(
        [FromRoute] Guid id,
        [FromBody] ChangePasswordRequest request)
    {
        request.UserId = id;
        await _userService.ChangePasswordAsync(request);
        return Ok(new SuccessResponse("Password changed successfully"));
    }
}
```

### 2. Model Documentation with Examples

Document API models with detailed schemas and examples:

```csharp
/// <summary>
/// User creation request
/// </summary>
/// <example>
/// {
///   "email": "john.doe@example.com",
///   "name": "John Doe",
///   "password": "SecurePassword123!",
///   "role": "User",
///   "phoneNumber": "+1234567890"
/// }
/// </example>
[SwaggerSchema(Description = "Request model for creating a new user account")]
public class CreateUserRequest
{
    /// <summary>
    /// User's email address (must be unique)
    /// </summary>
    /// <example>john.doe@example.com</example>
    [Required]
    [EmailAddress]
    [StringLength(256, MinimumLength = 5)]
    [SwaggerSchema(Description = "Valid email address that will be used for login")]
    public string Email { get; set; }

    /// <summary>
    /// User's full name
    /// </summary>
    /// <example>John Doe</example>
    [Required]
    [StringLength(100, MinimumLength = 2)]
    [SwaggerSchema(Description = "User's display name")]
    public string Name { get; set; }

    /// <summary>
    /// Secure password for the account
    /// </summary>
    /// <example>SecurePassword123!</example>
    [Required]
    [StringLength(128, MinimumLength = 8)]
    [SwaggerSchema(Description = "Password must contain uppercase, lowercase, digit and special character")]
    public string Password { get; set; }

    /// <summary>
    /// User role in the system
    /// </summary>
    /// <example>User</example>
    [SwaggerSchema(Description = "User's role determining access permissions")]
    public UserRole Role { get; set; } = UserRole.User;

    /// <summary>
    /// Optional phone number
    /// </summary>
    /// <example>+1234567890</example>
    [Phone]
    [SwaggerSchema(Description = "International format phone number")]
    public string PhoneNumber { get; set; }

    /// <summary>
    /// User preferences and settings
    /// </summary>
    [SwaggerSchema(Description = "Optional user preferences")]
    public UserPreferences Preferences { get; set; }
}

/// <summary>
/// User response model
/// </summary>
[SwaggerSchema(Description = "User information returned by the API")]
public class UserResponse
{
    /// <summary>
    /// Unique user identifier
    /// </summary>
    /// <example>123e4567-e89b-12d3-a456-426614174000</example>
    [SwaggerSchema(Description = "Unique identifier for the user")]
    public Guid Id { get; set; }

    /// <summary>
    /// User's email address
    /// </summary>
    /// <example>john.doe@example.com</example>
    [SwaggerSchema(Description = "User's email address")]
    public string Email { get; set; }

    /// <summary>
    /// User's display name
    /// </summary>
    /// <example>John Doe</example>
    [SwaggerSchema(Description = "User's full name")]
    public string Name { get; set; }

    /// <summary>
    /// User's role in the system
    /// </summary>
    /// <example>User</example>
    [SwaggerSchema(Description = "User's assigned role")]
    public UserRole Role { get; set; }

    /// <summary>
    /// Account status
    /// </summary>
    /// <example>true</example>
    [SwaggerSchema(Description = "Whether the user account is active")]
    public bool IsActive { get; set; }

    /// <summary>
    /// Account creation timestamp
    /// </summary>
    /// <example>2023-01-15T10:30:00Z</example>
    [SwaggerSchema(Description = "When the account was created")]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Last login timestamp
    /// </summary>
    /// <example>2023-01-20T14:45:00Z</example>
    [SwaggerSchema(Description = "When the user last logged in")]
    public DateTime? LastLoginAt { get; set; }

    public UserResponse(User user)
    {
        Id = user.Id;
        Email = user.Email;
        Name = user.Name;
        Role = user.Role;
        IsActive = user.IsActive;
        CreatedAt = user.CreatedAt;
        LastLoginAt = user.LastLoginAt;
    }
}

/// <summary>
/// Paginated response wrapper
/// </summary>
/// <typeparam name="T">Type of items in the page</typeparam>
[SwaggerSchema(Description = "Paginated response containing items and pagination metadata")]
public class PagedResponse<T>
{
    /// <summary>
    /// Items in the current page
    /// </summary>
    [SwaggerSchema(Description = "Array of items for the current page")]
    public IEnumerable<T> Items { get; set; }

    /// <summary>
    /// Total number of items across all pages
    /// </summary>
    /// <example>150</example>
    [SwaggerSchema(Description = "Total count of items matching the query")]
    public int TotalItems { get; set; }

    /// <summary>
    /// Current page number (1-based)
    /// </summary>
    /// <example>1</example>
    [SwaggerSchema(Description = "Current page number starting from 1")]
    public int Page { get; set; }

    /// <summary>
    /// Number of items per page
    /// </summary>
    /// <example>20</example>
    [SwaggerSchema(Description = "Number of items in each page")]
    public int PageSize { get; set; }

    /// <summary>
    /// Total number of pages
    /// </summary>
    /// <example>8</example>
    [SwaggerSchema(Description = "Total number of pages")]
    public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);

    /// <summary>
    /// Whether there is a previous page
    /// </summary>
    /// <example>false</example>
    [SwaggerSchema(Description = "True if there is a previous page")]
    public bool HasPrevious => Page > 1;

    /// <summary>
    /// Whether there is a next page
    /// </summary>
    /// <example>true</example>
    [SwaggerSchema(Description = "True if there is a next page")]
    public bool HasNext => Page < TotalPages;

    public PagedResponse(IEnumerable<T> items, int totalItems, int page, int pageSize)
    {
        Items = items;
        TotalItems = totalItems;
        Page = page;
        PageSize = pageSize;
    }
}

/// <summary>
/// User roles in the system
/// </summary>
[SwaggerSchema(Description = "Available user roles determining access permissions")]
public enum UserRole
{
    /// <summary>Regular user with basic permissions</summary>
    [SwaggerEnumInfo("Regular user with standard access")]
    User = 0,

    /// <summary>Manager with elevated permissions</summary>
    [SwaggerEnumInfo("Manager with team management capabilities")]
    Manager = 1,

    /// <summary>Administrator with full system access</summary>
    [SwaggerEnumInfo("Administrator with complete system access")]
    Admin = 2
}
```

### 3. Authentication Documentation

Document API authentication and security:

```csharp
// JWT Bearer authentication setup
services.AddSwagger(swagger =>
{
    swagger.AddSecurity = true;
    swagger.AddSecurityDefinitions = true;
    swagger.SecurityDefinitions = new Dictionary<string, SwaggerSecurityDefinition>
    {
        ["Bearer"] = new SwaggerSecurityDefinition
        {
            Type = SwaggerSecurityType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Enter JWT Bearer token in the format: Bearer {your-token}"
        },
        ["ApiKey"] = new SwaggerSecurityDefinition
        {
            Type = SwaggerSecurityType.ApiKey,
            Name = "X-API-Key",
            In = SwaggerSecurityLocation.Header,
            Description = "API Key for service-to-service authentication"
        }
    };

    swagger.GlobalSecurityRequirements = new[]
    {
        new SwaggerSecurityRequirement
        {
            ["Bearer"] = new string[] { }
        }
    };
});

// Authentication controller
/// <summary>
/// Authentication and authorization operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[SwaggerTag("Authentication", "User authentication and token management")]
public class AuthController : ControllerBase
{
    /// <summary>
    /// Authenticates user and returns JWT token
    /// </summary>
    /// <param name="request">Login credentials</param>
    /// <returns>JWT token and user information</returns>
    /// <remarks>
    /// Sample request:
    ///
    ///     POST /api/auth/login
    ///     {
    ///         "email": "john.doe@example.com",
    ///         "password": "SecurePassword123!"
    ///     }
    ///
    /// The returned JWT token should be included in subsequent requests as:
    ///
    ///     Authorization: Bearer {token}
    ///
    /// Token expires after 15 minutes for security.
    /// </remarks>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 401)]
    [SwaggerOperation(
        Summary = "User login",
        Description = "Authenticates user credentials and returns a JWT token",
        OperationId = "Login"
    )]
    [SwaggerResponse(200, "Login successful", typeof(AuthResponse))]
    [SwaggerResponse(401, "Invalid credentials", typeof(ErrorResponse))]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        // Implementation
        return Ok(new AuthResponse());
    }

    /// <summary>
    /// Refreshes an expired JWT token
    /// </summary>
    /// <param name="request">Refresh token</param>
    /// <returns>New JWT token</returns>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponse), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 401)]
    [SwaggerOperation(
        Summary = "Refresh token",
        Description = "Exchanges a refresh token for a new JWT access token",
        OperationId = "RefreshToken"
    )]
    public async Task<ActionResult<AuthResponse>> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        // Implementation
        return Ok(new AuthResponse());
    }

    /// <summary>
    /// Logs out user and invalidates token
    /// </summary>
    /// <returns>Success confirmation</returns>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(typeof(SuccessResponse), 200)]
    [SwaggerOperation(
        Summary = "User logout",
        Description = "Invalidates the current JWT token",
        OperationId = "Logout"
    )]
    [SwaggerSecurity("Bearer")]
    public async Task<ActionResult<SuccessResponse>> Logout()
    {
        // Implementation
        return Ok(new SuccessResponse("Logged out successfully"));
    }
}

/// <summary>
/// Login request model
/// </summary>
[SwaggerSchema(Description = "User login credentials")]
public class LoginRequest
{
    /// <summary>
    /// User's email address
    /// </summary>
    /// <example>john.doe@example.com</example>
    [Required]
    [EmailAddress]
    [SwaggerSchema(Description = "Registered email address")]
    public string Email { get; set; }

    /// <summary>
    /// User's password
    /// </summary>
    /// <example>SecurePassword123!</example>
    [Required]
    [SwaggerSchema(Description = "User's password")]
    public string Password { get; set; }

    /// <summary>
    /// Remember login for extended session
    /// </summary>
    /// <example>false</example>
    [SwaggerSchema(Description = "Whether to extend session duration")]
    public bool RememberMe { get; set; }
}

/// <summary>
/// Authentication response
/// </summary>
[SwaggerSchema(Description = "Authentication result with tokens and user info")]
public class AuthResponse
{
    /// <summary>
    /// JWT access token
    /// </summary>
    /// <example>eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...</example>
    [SwaggerSchema(Description = "JWT access token for API authentication")]
    public string AccessToken { get; set; }

    /// <summary>
    /// Refresh token for token renewal
    /// </summary>
    /// <example>rt_abc123xyz789</example>
    [SwaggerSchema(Description = "Refresh token for getting new access tokens")]
    public string RefreshToken { get; set; }

    /// <summary>
    /// Token expiration time in seconds
    /// </summary>
    /// <example>900</example>
    [SwaggerSchema(Description = "Access token lifetime in seconds")]
    public int ExpiresIn { get; set; }

    /// <summary>
    /// Token type
    /// </summary>
    /// <example>Bearer</example>
    [SwaggerSchema(Description = "Type of the access token")]
    public string TokenType { get; set; } = "Bearer";

    /// <summary>
    /// Authenticated user information
    /// </summary>
    [SwaggerSchema(Description = "Basic user information")]
    public UserSummaryResponse User { get; set; }
}
```

### 4. API Versioning Documentation

Support multiple API versions:

```csharp
// API versioning setup
services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new HeaderApiVersionReader("X-Version"),
        new QueryStringApiVersionReader("version")
    );
});

services.AddVersionedApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// Multiple Swagger documents
services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Version = "v1",
        Title = "Convey API v1",
        Description = "First version of the Convey API"
    });

    c.SwaggerDoc("v2", new OpenApiInfo
    {
        Version = "v2",
        Title = "Convey API v2",
        Description = "Second version with enhanced features"
    });
});

// Swagger UI with multiple versions
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Convey API v1");
    c.SwaggerEndpoint("/swagger/v2/swagger.json", "Convey API v2");
});

// Versioned controllers
/// <summary>
/// Users API v1
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[SwaggerTag("Users v1", "User management operations (version 1)")]
public class UsersV1Controller : ControllerBase
{
    /// <summary>
    /// Get user (v1 - basic info only)
    /// </summary>
    [HttpGet("{id:guid}")]
    [MapToApiVersion("1.0")]
    public async Task<ActionResult<UserResponseV1>> GetUser(Guid id)
    {
        // V1 implementation
        return Ok(new UserResponseV1());
    }
}

/// <summary>
/// Users API v2
/// </summary>
[ApiController]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[SwaggerTag("Users v2", "User management operations (version 2 - enhanced)")]
public class UsersV2Controller : ControllerBase
{
    /// <summary>
    /// Get user (v2 - extended information)
    /// </summary>
    [HttpGet("{id:guid}")]
    [MapToApiVersion("2.0")]
    public async Task<ActionResult<UserResponseV2>> GetUser(Guid id)
    {
        // V2 implementation with more features
        return Ok(new UserResponseV2());
    }
}
```

### 5. Custom Swagger Filters

Implement custom filters for enhanced documentation:

```csharp
// Custom operation filter
public class SwaggerOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // Add common response types
        if (!operation.Responses.ContainsKey("500"))
        {
            operation.Responses.Add("500", new OpenApiResponse
            {
                Description = "Internal server error",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        Schema = context.SchemaGenerator.GenerateSchema(typeof(ErrorResponse), context.SchemaRepository)
                    }
                }
            });
        }

        // Add security requirements for protected endpoints
        var hasAuthorize = context.MethodInfo.DeclaringType.GetCustomAttributes(true)
            .Union(context.MethodInfo.GetCustomAttributes(true))
            .OfType<AuthorizeAttribute>()
            .Any();

        if (hasAuthorize)
        {
            if (!operation.Responses.ContainsKey("401"))
            {
                operation.Responses.Add("401", new OpenApiResponse { Description = "Unauthorized" });
            }

            if (!operation.Responses.ContainsKey("403"))
            {
                operation.Responses.Add("403", new OpenApiResponse { Description = "Forbidden" });
            }

            operation.Security = new List<OpenApiSecurityRequirement>
            {
                new OpenApiSecurityRequirement
                {
                    [new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    }] = new string[] {}
                }
            };
        }

        // Add operation tags based on controller
        if (operation.Tags == null || !operation.Tags.Any())
        {
            var controllerName = context.MethodInfo.DeclaringType.Name.Replace("Controller", "");
            operation.Tags = new List<OpenApiTag> { new OpenApiTag { Name = controllerName } };
        }
    }
}

// Custom schema filter
public class SwaggerSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        // Add examples to enum schemas
        if (context.Type.IsEnum)
        {
            schema.Enum.Clear();
            foreach (var enumValue in Enum.GetValues(context.Type))
            {
                schema.Enum.Add(new OpenApiString(enumValue.ToString()));
            }
        }

        // Hide internal properties
        var propertiesToRemove = context.Type.GetProperties()
            .Where(p => p.GetCustomAttribute<SwaggerIgnoreAttribute>() != null)
            .Select(p => p.Name.ToCamelCase())
            .ToList();

        foreach (var property in propertiesToRemove)
        {
            if (schema.Properties.ContainsKey(property))
            {
                schema.Properties.Remove(property);
            }
        }
    }
}

// Register filters
services.AddSwaggerGen(c =>
{
    c.OperationFilter<SwaggerOperationFilter>();
    c.SchemaFilter<SwaggerSchemaFilter>();
    c.DocumentFilter<SwaggerDocumentFilter>();
});

// Custom document filter
public class SwaggerDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        // Remove unwanted paths
        var pathsToRemove = swaggerDoc.Paths
            .Where(p => p.Key.Contains("/internal/"))
            .Select(p => p.Key)
            .ToList();

        foreach (var path in pathsToRemove)
        {
            swaggerDoc.Paths.Remove(path);
        }

        // Add global headers
        foreach (var path in swaggerDoc.Paths.Values)
        {
            foreach (var operation in path.Operations.Values)
            {
                operation.Parameters ??= new List<OpenApiParameter>();

                if (!operation.Parameters.Any(p => p.Name == "X-Correlation-ID"))
                {
                    operation.Parameters.Add(new OpenApiParameter
                    {
                        Name = "X-Correlation-ID",
                        In = ParameterLocation.Header,
                        Required = false,
                        Description = "Correlation ID for request tracking",
                        Schema = new OpenApiSchema { Type = "string" }
                    });
                }
            }
        }
    }
}
```

## Configuration Options

### Swagger Options

```csharp
public class SwaggerOptions
{
    public string Title { get; set; }
    public string Description { get; set; }
    public string Version { get; set; }
    public string ContactName { get; set; }
    public string ContactEmail { get; set; }
    public string ContactUrl { get; set; }
    public string LicenseName { get; set; }
    public string LicenseUrl { get; set; }
    public string TermsOfService { get; set; }
    public bool IncludeXmlComments { get; set; }
    public string XmlCommentsFilePath { get; set; }
    public bool EnableAnnotations { get; set; }
    public bool EnableFilters { get; set; }
    public bool SerializeAsV2 { get; set; }
    public SwaggerServer[] Servers { get; set; }
    public SwaggerTag[] Tags { get; set; }
}

public class SwaggerServer
{
    public string Url { get; set; }
    public string Description { get; set; }
}

public class SwaggerTag
{
    public string Name { get; set; }
    public string Description { get; set; }
}
```

## API Reference

### Extension Methods

```csharp
public static class ConveyExtensions
{
    public static IConveyBuilder AddSwagger(this IConveyBuilder builder, Action<SwaggerOptions> configure = null);
    public static IApplicationBuilder UseSwagger(this IApplicationBuilder app, Action<SwaggerUIOptions> configure = null);
}
```

## Best Practices

1. **Use XML comments** - Provide comprehensive API documentation
2. **Include examples** - Add request/response examples for clarity
3. **Document authentication** - Clearly explain security requirements
4. **Version your APIs** - Support multiple API versions
5. **Use consistent naming** - Follow RESTful naming conventions
6. **Add validation attributes** - Use data annotations for automatic validation
7. **Hide internal APIs** - Don't expose internal endpoints
8. **Provide operation IDs** - Use consistent operation identifiers

## Troubleshooting

### Common Issues

1. **Missing XML comments**
   - Ensure XML documentation generation is enabled
   - Check XML comments file path configuration
   - Verify file is included in output directory

2. **Authentication not working**
   - Check security scheme configuration
   - Verify JWT token format
   - Ensure proper authorization attributes

3. **Models not documented**
   - Check schema generation settings
   - Verify model attributes are correct
   - Ensure proper JSON serialization

4. **Swagger UI not loading**
   - Check route configuration
   - Verify middleware order
   - Ensure static files are served correctly

