---
layout: default
title: Convey.Docs.Swagger
parent: HTTP & API
---
# Convey.Docs.Swagger

Swagger/OpenAPI documentation generation and UI integration providing comprehensive API documentation with authentication, request/response examples, and interactive testing capabilities.

## Installation

```bash
dotnet add package Convey.Docs.Swagger
```

## Overview

Convey.Docs.Swagger provides:
- **OpenAPI specification** - Automatic API documentation generation
- **Swagger UI** - Interactive API documentation interface
- **Authentication integration** - JWT and API key authentication documentation
- **Request/response examples** - Comprehensive examples and schemas
- **Customizable themes** - Flexible UI customization options
- **Multiple versions** - API versioning support
- **Export capabilities** - Export documentation in various formats
- **Security definitions** - Detailed security scheme documentation

## Configuration

### Basic Swagger Setup

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddWebApi()
    .AddSwaggerDocs();

var app = builder.Build();

app.UseSwaggerDocs();
app.Run();
```

### Advanced Configuration

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddWebApi()
    .AddSwaggerDocs(swagger =>
    {
        swagger.Title = "User Management API";
        swagger.Name = "user-api";
        swagger.Version = "v1";
        swagger.Description = "Comprehensive API for user management operations";
        swagger.RoutePrefix = "docs";
        swagger.IncludeSecurity = true;
        swagger.SerializerSettings = new JsonSerializerSettings
        {
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.Indented
        };
        swagger.Contact = new Contact
        {
            Name = "API Support",
            Email = "support@example.com",
            Url = "https://example.com/support"
        };
        swagger.License = new License
        {
            Name = "MIT",
            Url = "https://opensource.org/licenses/MIT"
        };
        swagger.TermsOfService = "https://example.com/terms";
        swagger.Tags = new[]
        {
            new Tag { Name = "Users", Description = "User management operations" },
            new Tag { Name = "Auth", Description = "Authentication and authorization" },
            new Tag { Name = "Admin", Description = "Administrative operations" }
        };
    });

var app = builder.Build();

app.UseSwaggerDocs();
app.Run();
```

### Multiple API Versions

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddWebApi()
    .AddSwaggerDocs("v1", swagger =>
    {
        swagger.Title = "User Management API";
        swagger.Name = "user-api-v1";
        swagger.Version = "v1";
        swagger.Description = "Version 1 of the User Management API";
        swagger.RoutePrefix = "docs/v1";
        swagger.IncludeSecurity = true;
    })
    .AddSwaggerDocs("v2", swagger =>
    {
        swagger.Title = "User Management API";
        swagger.Name = "user-api-v2";
        swagger.Version = "v2";
        swagger.Description = "Version 2 of the User Management API with enhanced features";
        swagger.RoutePrefix = "docs/v2";
        swagger.IncludeSecurity = true;
        swagger.DeprecatedVersions = new[] { "v1" };
    });

var app = builder.Build();

app.UseSwaggerDocs("v1");
app.UseSwaggerDocs("v2");
app.Run();
```

### Security Schemes Configuration

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddWebApi()
    .AddSwaggerDocs(swagger =>
    {
        swagger.Title = "Secure API";
        swagger.Name = "secure-api";
        swagger.Version = "v1";
        swagger.IncludeSecurity = true;
        swagger.SecurityDefinitions = new Dictionary<string, SecurityScheme>
        {
            ["Bearer"] = new SecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Enter JWT Bearer token"
            },
            ["ApiKey"] = new SecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Name = "X-API-Key",
                Description = "Enter API key"
            },
            ["OAuth2"] = new SecurityScheme
            {
                Type = SecuritySchemeType.OAuth2,
                Flows = new OAuth2Flows
                {
                    AuthorizationCode = new OAuth2Flow
                    {
                        AuthorizationUrl = "https://auth.example.com/oauth/authorize",
                        TokenUrl = "https://auth.example.com/oauth/token",
                        Scopes = new Dictionary<string, string>
                        {
                            ["read"] = "Read access",
                            ["write"] = "Write access",
                            ["admin"] = "Administrative access"
                        }
                    }
                }
            }
        };
    });

var app = builder.Build();

app.UseSwaggerDocs();
app.Run();
```

## Key Features

### 1. Comprehensive API Documentation

Automatic documentation generation with detailed examples:

```csharp
/// <summary>
/// Manages user accounts and profiles
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
[Tags("Users")]
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
    /// <response code="201">User created successfully</response>
    /// <response code="400">Invalid user data provided</response>
    /// <response code="409">User with email already exists</response>
    [HttpPost]
    [ProducesResponseType(typeof(UserResponse), 201)]
    [ProducesResponseType(typeof(ErrorResponse), 400)]
    [ProducesResponseType(typeof(ErrorResponse), 409)]
    [SwaggerOperation(
        Summary = "Create user",
        Description = "Creates a new user account with the provided information",
        OperationId = "CreateUser",
        Tags = new[] { "Users", "Management" }
    )]
    [SwaggerRequestExample(typeof(CreateUserRequest), typeof(CreateUserRequestExample))]
    [SwaggerResponseExample(201, typeof(UserResponseExample))]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        var user = await _userService.CreateUserAsync(request);
        var response = new UserResponse
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role,
            CreatedAt = user.CreatedAt,
            IsActive = user.IsActive
        };

        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, response);
    }

    /// <summary>
    /// Retrieves a user by ID
    /// </summary>
    /// <param name="id">User identifier</param>
    /// <returns>User information</returns>
    /// <response code="200">User found and returned</response>
    /// <response code="404">User not found</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(UserResponse), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    [SwaggerOperation(
        Summary = "Get user by ID",
        Description = "Retrieves detailed information about a specific user",
        OperationId = "GetUser"
    )]
    [SwaggerResponseExample(200, typeof(UserResponseExample))]
    public async Task<IActionResult> GetUser([FromRoute] Guid id)
    {
        var user = await _userService.GetUserByIdAsync(id);
        if (user == null)
        {
            return NotFound(new ErrorResponse { Message = "User not found", Code = "USER_NOT_FOUND" });
        }

        var response = new UserResponse
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            IsActive = user.IsActive
        };

        return Ok(response);
    }

    /// <summary>
    /// Retrieves a paginated list of users
    /// </summary>
    /// <param name="searchTerm">Optional search term for filtering users</param>
    /// <param name="role">Optional role filter</param>
    /// <param name="isActive">Optional active status filter</param>
    /// <param name="page">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <returns>Paginated list of users</returns>
    /// <response code="200">Users retrieved successfully</response>
    /// <response code="400">Invalid query parameters</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<UserResponse>), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 400)]
    [SwaggerOperation(
        Summary = "Get users",
        Description = "Retrieves a paginated list of users with optional filtering",
        OperationId = "GetUsers"
    )]
    [SwaggerParameter("searchTerm", "Search term to filter users by name or email")]
    [SwaggerParameter("role", "Filter users by role")]
    [SwaggerParameter("isActive", "Filter users by active status")]
    [SwaggerParameter("page", "Page number (default: 1)")]
    [SwaggerParameter("pageSize", "Items per page (default: 20, max: 100)")]
    [SwaggerResponseExample(200, typeof(PagedUserResponseExample))]
    public async Task<IActionResult> GetUsers(
        [FromQuery] string? searchTerm = null,
        [FromQuery] UserRole? role = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var result = await _userService.GetUsersAsync(searchTerm, role, isActive, page, pageSize);

        var response = new PagedResponse<UserResponse>
        {
            Items = result.Items.Select(u => new UserResponse
            {
                Id = u.Id,
                Email = u.Email,
                FirstName = u.FirstName,
                LastName = u.LastName,
                Role = u.Role,
                CreatedAt = u.CreatedAt,
                UpdatedAt = u.UpdatedAt,
                IsActive = u.IsActive
            }).ToList(),
            TotalCount = result.TotalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling((double)result.TotalCount / pageSize)
        };

        return Ok(response);
    }

    /// <summary>
    /// Updates an existing user
    /// </summary>
    /// <param name="id">User identifier</param>
    /// <param name="request">Updated user information</param>
    /// <returns>Updated user information</returns>
    /// <response code="200">User updated successfully</response>
    /// <response code="400">Invalid user data provided</response>
    /// <response code="404">User not found</response>
    /// <response code="409">Email already in use by another user</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(UserResponse), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 400)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    [ProducesResponseType(typeof(ErrorResponse), 409)]
    [SwaggerOperation(
        Summary = "Update user",
        Description = "Updates an existing user with the provided information",
        OperationId = "UpdateUser"
    )]
    [SwaggerRequestExample(typeof(UpdateUserRequest), typeof(UpdateUserRequestExample))]
    [SwaggerResponseExample(200, typeof(UserResponseExample))]
    public async Task<IActionResult> UpdateUser([FromRoute] Guid id, [FromBody] UpdateUserRequest request)
    {
        var user = await _userService.UpdateUserAsync(id, request);
        if (user == null)
        {
            return NotFound(new ErrorResponse { Message = "User not found", Code = "USER_NOT_FOUND" });
        }

        var response = new UserResponse
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            IsActive = user.IsActive
        };

        return Ok(response);
    }

    /// <summary>
    /// Deletes a user account
    /// </summary>
    /// <param name="id">User identifier</param>
    /// <param name="softDelete">Whether to perform soft delete (default: true)</param>
    /// <returns>No content on successful deletion</returns>
    /// <response code="204">User deleted successfully</response>
    /// <response code="404">User not found</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    [SwaggerOperation(
        Summary = "Delete user",
        Description = "Deletes a user account (soft delete by default)",
        OperationId = "DeleteUser"
    )]
    [SwaggerParameter("softDelete", "Whether to perform soft delete (default: true)")]
    public async Task<IActionResult> DeleteUser([FromRoute] Guid id, [FromQuery] bool softDelete = true)
    {
        var deleted = await _userService.DeleteUserAsync(id, softDelete);
        if (!deleted)
        {
            return NotFound(new ErrorResponse { Message = "User not found", Code = "USER_NOT_FOUND" });
        }

        return NoContent();
    }
}
```

### 2. Request/Response Examples

Comprehensive examples for API documentation:

```csharp
// Request DTOs with validation attributes and examples
/// <summary>
/// Request model for creating a new user
/// </summary>
[SwaggerSchema(Description = "User creation request with required and optional fields")]
public class CreateUserRequest
{
    /// <summary>
    /// User's email address (must be unique)
    /// </summary>
    /// <example>john.doe@example.com</example>
    [Required]
    [EmailAddress]
    [SwaggerSchema(Description = "Valid email address that will be used as the username")]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// User's first name
    /// </summary>
    /// <example>John</example>
    [Required]
    [StringLength(50, MinimumLength = 1)]
    [SwaggerSchema(Description = "First name of the user (1-50 characters)")]
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// User's last name
    /// </summary>
    /// <example>Doe</example>
    [Required]
    [StringLength(50, MinimumLength = 1)]
    [SwaggerSchema(Description = "Last name of the user (1-50 characters)")]
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// User's role in the system
    /// </summary>
    /// <example>User</example>
    [Required]
    [SwaggerSchema(Description = "Role that determines user permissions")]
    public UserRole Role { get; set; } = UserRole.User;

    /// <summary>
    /// User's date of birth (optional)
    /// </summary>
    /// <example>1990-01-15</example>
    [SwaggerSchema(Description = "Date of birth in ISO 8601 format")]
    public DateTime? DateOfBirth { get; set; }

    /// <summary>
    /// User's phone number (optional)
    /// </summary>
    /// <example>+1-555-123-4567</example>
    [Phone]
    [SwaggerSchema(Description = "Phone number in international format")]
    public string? PhoneNumber { get; set; }
}

/// <summary>
/// Request model for updating an existing user
/// </summary>
[SwaggerSchema(Description = "User update request with optional fields")]
public class UpdateUserRequest
{
    /// <summary>
    /// Updated email address
    /// </summary>
    /// <example>john.updated@example.com</example>
    [EmailAddress]
    [SwaggerSchema(Description = "New email address (must be unique if provided)")]
    public string? Email { get; set; }

    /// <summary>
    /// Updated first name
    /// </summary>
    /// <example>Jonathan</example>
    [StringLength(50, MinimumLength = 1)]
    [SwaggerSchema(Description = "Updated first name (1-50 characters)")]
    public string? FirstName { get; set; }

    /// <summary>
    /// Updated last name
    /// </summary>
    /// <example>Smith</example>
    [StringLength(50, MinimumLength = 1)]
    [SwaggerSchema(Description = "Updated last name (1-50 characters)")]
    public string? LastName { get; set; }

    /// <summary>
    /// Updated phone number
    /// </summary>
    /// <example>+1-555-987-6543</example>
    [Phone]
    [SwaggerSchema(Description = "Updated phone number in international format")]
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// Whether the user account is active
    /// </summary>
    /// <example>true</example>
    [SwaggerSchema(Description = "Account status - inactive users cannot log in")]
    public bool? IsActive { get; set; }
}

// Response DTOs with examples
/// <summary>
/// User information response
/// </summary>
[SwaggerSchema(Description = "Complete user information")]
public class UserResponse
{
    /// <summary>
    /// Unique user identifier
    /// </summary>
    /// <example>123e4567-e89b-12d3-a456-426614174000</example>
    [SwaggerSchema(Description = "UUID that uniquely identifies the user")]
    public Guid Id { get; set; }

    /// <summary>
    /// User's email address
    /// </summary>
    /// <example>john.doe@example.com</example>
    [SwaggerSchema(Description = "User's email address")]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// User's first name
    /// </summary>
    /// <example>John</example>
    [SwaggerSchema(Description = "User's first name")]
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// User's last name
    /// </summary>
    /// <example>Doe</example>
    [SwaggerSchema(Description = "User's last name")]
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// User's role in the system
    /// </summary>
    /// <example>User</example>
    [SwaggerSchema(Description = "User's assigned role")]
    public UserRole Role { get; set; }

    /// <summary>
    /// User's phone number
    /// </summary>
    /// <example>+1-555-123-4567</example>
    [SwaggerSchema(Description = "User's phone number")]
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// When the user account was created
    /// </summary>
    /// <example>2023-01-15T10:30:00Z</example>
    [SwaggerSchema(Description = "Account creation timestamp")]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the user account was last updated
    /// </summary>
    /// <example>2023-01-20T14:45:00Z</example>
    [SwaggerSchema(Description = "Last update timestamp")]
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Whether the user account is active
    /// </summary>
    /// <example>true</example>
    [SwaggerSchema(Description = "Account status")]
    public bool IsActive { get; set; }
}

/// <summary>
/// Paginated response wrapper
/// </summary>
/// <typeparam name="T">Type of items in the response</typeparam>
[SwaggerSchema(Description = "Paginated response containing items and pagination metadata")]
public class PagedResponse<T>
{
    /// <summary>
    /// List of items for the current page
    /// </summary>
    [SwaggerSchema(Description = "Items for the current page")]
    public List<T> Items { get; set; } = new();

    /// <summary>
    /// Total number of items across all pages
    /// </summary>
    /// <example>150</example>
    [SwaggerSchema(Description = "Total count of items")]
    public int TotalCount { get; set; }

    /// <summary>
    /// Current page number (1-based)
    /// </summary>
    /// <example>1</example>
    [SwaggerSchema(Description = "Current page number")]
    public int Page { get; set; }

    /// <summary>
    /// Number of items per page
    /// </summary>
    /// <example>20</example>
    [SwaggerSchema(Description = "Items per page")]
    public int PageSize { get; set; }

    /// <summary>
    /// Total number of pages
    /// </summary>
    /// <example>8</example>
    [SwaggerSchema(Description = "Total number of pages")]
    public int TotalPages { get; set; }
}

/// <summary>
/// Standard error response
/// </summary>
[SwaggerSchema(Description = "Error response with details")]
public class ErrorResponse
{
    /// <summary>
    /// Error message describing what went wrong
    /// </summary>
    /// <example>User not found</example>
    [SwaggerSchema(Description = "Human-readable error message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Machine-readable error code
    /// </summary>
    /// <example>USER_NOT_FOUND</example>
    [SwaggerSchema(Description = "Error code for programmatic handling")]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Additional error details (optional)
    /// </summary>
    [SwaggerSchema(Description = "Additional error information")]
    public Dictionary<string, object>? Details { get; set; }
}

// Swagger examples
public class CreateUserRequestExample : IExamplesProvider<CreateUserRequest>
{
    public CreateUserRequest GetExamples()
    {
        return new CreateUserRequest
        {
            Email = "john.doe@example.com",
            FirstName = "John",
            LastName = "Doe",
            Role = UserRole.User,
            DateOfBirth = new DateTime(1990, 1, 15),
            PhoneNumber = "+1-555-123-4567"
        };
    }
}

public class UpdateUserRequestExample : IExamplesProvider<UpdateUserRequest>
{
    public UpdateUserRequest GetExamples()
    {
        return new UpdateUserRequest
        {
            Email = "john.updated@example.com",
            FirstName = "Jonathan",
            LastName = "Smith",
            PhoneNumber = "+1-555-987-6543",
            IsActive = true
        };
    }
}

public class UserResponseExample : IExamplesProvider<UserResponse>
{
    public UserResponse GetExamples()
    {
        return new UserResponse
        {
            Id = Guid.Parse("123e4567-e89b-12d3-a456-426614174000"),
            Email = "john.doe@example.com",
            FirstName = "John",
            LastName = "Doe",
            Role = UserRole.User,
            PhoneNumber = "+1-555-123-4567",
            CreatedAt = DateTime.Parse("2023-01-15T10:30:00Z"),
            UpdatedAt = DateTime.Parse("2023-01-20T14:45:00Z"),
            IsActive = true
        };
    }
}

public class PagedUserResponseExample : IExamplesProvider<PagedResponse<UserResponse>>
{
    public PagedResponse<UserResponse> GetExamples()
    {
        return new PagedResponse<UserResponse>
        {
            Items = new List<UserResponse>
            {
                new UserResponse
                {
                    Id = Guid.Parse("123e4567-e89b-12d3-a456-426614174000"),
                    Email = "john.doe@example.com",
                    FirstName = "John",
                    LastName = "Doe",
                    Role = UserRole.User,
                    CreatedAt = DateTime.Parse("2023-01-15T10:30:00Z"),
                    IsActive = true
                },
                new UserResponse
                {
                    Id = Guid.Parse("987fcdeb-51a2-43d1-9abc-123456789def"),
                    Email = "jane.smith@example.com",
                    FirstName = "Jane",
                    LastName = "Smith",
                    Role = UserRole.Admin,
                    CreatedAt = DateTime.Parse("2023-01-10T09:15:00Z"),
                    IsActive = true
                }
            },
            TotalCount = 150,
            Page = 1,
            PageSize = 20,
            TotalPages = 8
        };
    }
}
```

### 3. Authentication Documentation

Documenting API authentication and security:

```csharp
/// <summary>
/// Authentication and authorization endpoints
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
[Tags("Authentication")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Authenticates user and returns JWT token
    /// </summary>
    /// <param name="request">Login credentials</param>
    /// <returns>JWT token and user information</returns>
    /// <response code="200">Authentication successful</response>
    /// <response code="401">Invalid credentials</response>
    /// <response code="423">Account locked due to too many failed attempts</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 401)]
    [ProducesResponseType(typeof(ErrorResponse), 423)]
    [SwaggerOperation(
        Summary = "User login",
        Description = "Authenticates user credentials and returns a JWT token for API access",
        OperationId = "Login"
    )]
    [SwaggerRequestExample(typeof(LoginRequest), typeof(LoginRequestExample))]
    [SwaggerResponseExample(200, typeof(AuthResponseExample))]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request.Email, request.Password);

        if (!result.Success)
        {
            return Unauthorized(new ErrorResponse
            {
                Message = result.Message,
                Code = result.ErrorCode
            });
        }

        var response = new AuthResponse
        {
            Token = result.Token,
            ExpiresAt = result.ExpiresAt,
            RefreshToken = result.RefreshToken,
            User = new UserResponse
            {
                Id = result.User.Id,
                Email = result.User.Email,
                FirstName = result.User.FirstName,
                LastName = result.User.LastName,
                Role = result.User.Role,
                IsActive = result.User.IsActive
            }
        };

        return Ok(response);
    }

    /// <summary>
    /// Refreshes an expired JWT token
    /// </summary>
    /// <param name="request">Refresh token request</param>
    /// <returns>New JWT token</returns>
    /// <response code="200">Token refreshed successfully</response>
    /// <response code="401">Invalid refresh token</response>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponse), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 401)]
    [SwaggerOperation(
        Summary = "Refresh token",
        Description = "Exchanges a valid refresh token for a new JWT token",
        OperationId = "RefreshToken"
    )]
    [SwaggerRequestExample(typeof(RefreshTokenRequest), typeof(RefreshTokenRequestExample))]
    [SwaggerResponseExample(200, typeof(AuthResponseExample))]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        var result = await _authService.RefreshTokenAsync(request.RefreshToken);

        if (!result.Success)
        {
            return Unauthorized(new ErrorResponse
            {
                Message = result.Message,
                Code = result.ErrorCode
            });
        }

        var response = new AuthResponse
        {
            Token = result.Token,
            ExpiresAt = result.ExpiresAt,
            RefreshToken = result.RefreshToken,
            User = new UserResponse
            {
                Id = result.User.Id,
                Email = result.User.Email,
                FirstName = result.User.FirstName,
                LastName = result.User.LastName,
                Role = result.User.Role,
                IsActive = result.User.IsActive
            }
        };

        return Ok(response);
    }

    /// <summary>
    /// Logs out user and invalidates refresh token
    /// </summary>
    /// <returns>Logout confirmation</returns>
    /// <response code="200">Logout successful</response>
    /// <response code="401">User not authenticated</response>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(typeof(ErrorResponse), 401)]
    [SwaggerOperation(
        Summary = "User logout",
        Description = "Logs out the current user and invalidates their refresh token",
        OperationId = "Logout"
    )]
    [SwaggerSecurity("Bearer")]
    public async Task<IActionResult> Logout()
    {
        var userId = User.GetUserId();
        await _authService.LogoutAsync(userId);

        return Ok(new { Message = "Logout successful" });
    }

    /// <summary>
    /// Gets current user profile information
    /// </summary>
    /// <returns>Current user information</returns>
    /// <response code="200">User profile retrieved</response>
    /// <response code="401">User not authenticated</response>
    [HttpGet("profile")]
    [Authorize]
    [ProducesResponseType(typeof(UserResponse), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 401)]
    [SwaggerOperation(
        Summary = "Get user profile",
        Description = "Retrieves the profile information of the currently authenticated user",
        OperationId = "GetProfile"
    )]
    [SwaggerSecurity("Bearer")]
    [SwaggerResponseExample(200, typeof(UserResponseExample))]
    public async Task<IActionResult> GetProfile()
    {
        var userId = User.GetUserId();
        var user = await _authService.GetUserProfileAsync(userId);

        var response = new UserResponse
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role,
            PhoneNumber = user.PhoneNumber,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            IsActive = user.IsActive
        };

        return Ok(response);
    }
}

// Authentication DTOs
/// <summary>
/// Login request with user credentials
/// </summary>
[SwaggerSchema(Description = "User login credentials")]
public class LoginRequest
{
    /// <summary>
    /// User's email address
    /// </summary>
    /// <example>user@example.com</example>
    [Required]
    [EmailAddress]
    [SwaggerSchema(Description = "Registered email address")]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// User's password
    /// </summary>
    /// <example>SecurePassword123!</example>
    [Required]
    [SwaggerSchema(Description = "User password")]
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Refresh token request
/// </summary>
[SwaggerSchema(Description = "Request to refresh an expired JWT token")]
public class RefreshTokenRequest
{
    /// <summary>
    /// Valid refresh token
    /// </summary>
    /// <example>eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...</example>
    [Required]
    [SwaggerSchema(Description = "Refresh token obtained from login")]
    public string RefreshToken { get; set; } = string.Empty;
}

/// <summary>
/// Authentication response with tokens and user info
/// </summary>
[SwaggerSchema(Description = "Successful authentication response")]
public class AuthResponse
{
    /// <summary>
    /// JWT access token
    /// </summary>
    /// <example>eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...</example>
    [SwaggerSchema(Description = "JWT token for API authentication")]
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Token expiration time
    /// </summary>
    /// <example>2023-01-15T11:30:00Z</example>
    [SwaggerSchema(Description = "When the JWT token expires")]
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Refresh token for obtaining new JWT tokens
    /// </summary>
    /// <example>refresh_eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...</example>
    [SwaggerSchema(Description = "Token for refreshing expired JWT")]
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    /// Authenticated user information
    /// </summary>
    [SwaggerSchema(Description = "User information")]
    public UserResponse User { get; set; } = new();
}

// Swagger examples for authentication
public class LoginRequestExample : IExamplesProvider<LoginRequest>
{
    public LoginRequest GetExamples()
    {
        return new LoginRequest
        {
            Email = "user@example.com",
            Password = "SecurePassword123!"
        };
    }
}

public class RefreshTokenRequestExample : IExamplesProvider<RefreshTokenRequest>
{
    public RefreshTokenRequest GetExamples()
    {
        return new RefreshTokenRequest
        {
            RefreshToken = "refresh_eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c"
        };
    }
}

public class AuthResponseExample : IExamplesProvider<AuthResponse>
{
    public AuthResponse GetExamples()
    {
        return new AuthResponse
        {
            Token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            RefreshToken = "refresh_eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c",
            User = new UserResponse
            {
                Id = Guid.Parse("123e4567-e89b-12d3-a456-426614174000"),
                Email = "user@example.com",
                FirstName = "John",
                LastName = "Doe",
                Role = UserRole.User,
                IsActive = true
            }
        };
    }
}
```

### 4. Custom Swagger UI Configuration

Advanced UI customization and branding:

```csharp
public class SwaggerUICustomization
{
    public static void ConfigureSwaggerUI(SwaggerUIOptions options)
    {
        // Basic configuration
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "User Management API v1");
        options.RoutePrefix = "docs";

        // UI customization
        options.DocumentTitle = "User Management API Documentation";
        options.DefaultModelExpandDepth(2);
        options.DefaultModelRendering(ModelRendering.Example);
        options.DefaultModelsExpandDepth(-1);
        options.DisplayOperationId();
        options.DisplayRequestDuration();
        options.EnableDeepLinking();
        options.EnableFilter();
        options.EnableValidator();
        options.ShowExtensions();
        options.ShowCommonExtensions();
        options.UseRequestInterceptor("(request) => { console.log('Request:', request); return request; }");
        options.UseResponseInterceptor("(response) => { console.log('Response:', response); return response; }");

        // Custom CSS and JavaScript
        options.InjectStylesheet("/swagger-ui/custom.css");
        options.InjectJavascript("/swagger-ui/custom.js", "text/javascript");

        // OAuth configuration
        options.OAuthClientId("swagger-ui");
        options.OAuthClientSecret("swagger-ui-secret");
        options.OAuthRealm("swagger-ui-realm");
        options.OAuthAppName("Swagger UI");
        options.OAuthScopeSeparator(" ");
        options.OAuthAdditionalQueryStringParams(new Dictionary<string, string>
        {
            { "resource", "api" }
        });
        options.OAuthUseBasicAuthenticationWithAccessCodeGrant();

        // Configure supported submit methods
        options.SupportedSubmitMethods(SubmitMethod.Get, SubmitMethod.Post, SubmitMethod.Put, SubmitMethod.Delete, SubmitMethod.Patch);

        // Configure try it out
        options.EnableTryItOutByDefault();
        options.ConfigObject.AdditionalItems.Add("syntaxHighlight", new Dictionary<string, object>
        {
            ["activated"] = true,
            ["theme"] = "agate"
        });

        options.ConfigObject.AdditionalItems.Add("requestSnippets", new Dictionary<string, object>
        {
            ["generators"] = new Dictionary<string, object>
            {
                ["curl_bash"] = new Dictionary<string, object>
                {
                    ["title"] = "cURL (bash)",
                    ["syntax"] = "bash"
                },
                ["curl_powershell"] = new Dictionary<string, object>
                {
                    ["title"] = "cURL (PowerShell)",
                    ["syntax"] = "powershell"
                },
                ["curl_cmd"] = new Dictionary<string, object>
                {
                    ["title"] = "cURL (CMD)",
                    ["syntax"] = "bash"
                }
            },
            ["defaultExpanded"] = true,
            ["languages"] = new[] { "curl_bash", "curl_powershell", "curl_cmd" }
        });
    }
}

// Custom CSS content (wwwroot/swagger-ui/custom.css)
/*
.swagger-ui .topbar {
    background-color: #2c3e50;
}

.swagger-ui .topbar .download-url-wrapper {
    display: none;
}

.swagger-ui .info .title {
    color: #2c3e50;
}

.swagger-ui .info .description {
    color: #34495e;
}

.swagger-ui .scheme-container {
    background: #ecf0f1;
    border-radius: 4px;
    padding: 15px;
}

.swagger-ui .opblock.opblock-post {
    border-color: #27ae60;
    background: rgba(39, 174, 96, 0.1);
}

.swagger-ui .opblock.opblock-get {
    border-color: #3498db;
    background: rgba(52, 152, 219, 0.1);
}

.swagger-ui .opblock.opblock-put {
    border-color: #f39c12;
    background: rgba(243, 156, 18, 0.1);
}

.swagger-ui .opblock.opblock-delete {
    border-color: #e74c3c;
    background: rgba(231, 76, 60, 0.1);
}
*/

// Custom JavaScript content (wwwroot/swagger-ui/custom.js)
/*
window.onload = function() {
    // Add custom headers
    window.ui.preauthorizeApiKey('Bearer', 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...');

    // Custom request interceptor
    window.ui.getConfigs().requestInterceptor = (request) => {
        // Add custom headers
        request.headers['X-API-Version'] = 'v1';
        request.headers['X-Client-Id'] = 'swagger-ui';

        console.log('Intercepted request:', request);
        return request;
    };

    // Custom response interceptor
    window.ui.getConfigs().responseInterceptor = (response) => {
        console.log('Intercepted response:', response);
        return response;
    };

    // Auto-expand operations
    setTimeout(() => {
        document.querySelectorAll('.opblock-summary').forEach(el => {
            if (!el.parentElement.classList.contains('is-open')) {
                el.click();
            }
        });
    }, 1000);
};
*/
```

## Configuration Options

### Swagger Options

```csharp
public class SwaggerOptions
{
    public string Title { get; set; } = "API";
    public string Name { get; set; } = "api";
    public string Version { get; set; } = "v1";
    public string Description { get; set; } = string.Empty;
    public string RoutePrefix { get; set; } = "swagger";
    public bool IncludeSecurity { get; set; } = false;
    public JsonSerializerSettings SerializerSettings { get; set; } = new();
    public Contact Contact { get; set; } = new();
    public License License { get; set; } = new();
    public string TermsOfService { get; set; } = string.Empty;
    public Tag[] Tags { get; set; } = Array.Empty<Tag>();
    public Dictionary<string, SecurityScheme> SecurityDefinitions { get; set; } = new();
    public string[] DeprecatedVersions { get; set; } = Array.Empty<string>();
}
```

## API Reference

### Extension Methods

```csharp
public static class ConveyExtensions
{
    public static IConveyBuilder AddSwaggerDocs(this IConveyBuilder builder);
    public static IConveyBuilder AddSwaggerDocs(this IConveyBuilder builder, Action<SwaggerOptions> configure);
    public static IConveyBuilder AddSwaggerDocs(this IConveyBuilder builder, string sectionName);
    public static IConveyBuilder AddSwaggerDocs(this IConveyBuilder builder, string name, Action<SwaggerOptions> configure);
}

public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseSwaggerDocs(this IApplicationBuilder app);
    public static IApplicationBuilder UseSwaggerDocs(this IApplicationBuilder app, string name);
}
```

## Best Practices

1. **Use comprehensive documentation** - Document all endpoints with detailed descriptions
2. **Provide examples** - Include request/response examples for all operations
3. **Implement proper security** - Document authentication and authorization requirements
4. **Version your APIs** - Support multiple API versions with clear documentation
5. **Use meaningful tags** - Organize endpoints with logical groupings
6. **Validate examples** - Ensure all examples are valid and up-to-date
7. **Customize UI appropriately** - Brand the documentation to match your organization
8. **Keep documentation current** - Regularly update documentation with API changes

## Troubleshooting

### Common Issues

1. **Missing endpoint documentation**
   - Ensure controllers are properly decorated with attributes
   - Verify XML documentation is enabled
   - Check namespace and assembly inclusion

2. **Authentication not working in UI**
   - Verify security schemes are properly configured
   - Check JWT token format and claims
   - Ensure CORS is configured for Swagger UI

3. **Examples not displaying**
   - Verify example providers implement correct interfaces
   - Check example data matches model schemas
   - Ensure examples are registered in DI container

4. **UI customization issues**
   - Verify custom CSS/JS files are served correctly
   - Check browser console for JavaScript errors
   - Ensure file paths and MIME types are correct

