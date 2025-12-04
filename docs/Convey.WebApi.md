---
layout: default
title: Convey.WebApi
parent: Foundation
---
# Convey.WebApi

Provides Web API extensions and minimal routing capabilities for building RESTful APIs with ASP.NET Core. Offers a fluent API for defining endpoints with built-in request/response handling.

## Installation

```bash
dotnet add package Convey.WebApi
```

## Overview

Convey.WebApi provides:
- **Minimal routing API** - Fluent endpoint definition without controllers
- **Automatic request binding** - Route and body parameter binding
- **Response formatting** - Consistent JSON serialization
- **Exception handling** - Global error handling middleware
- **Authentication integration** - Built-in auth support for endpoints
- **OpenAPI integration** - Automatic endpoint documentation

## Configuration

### Basic Setup

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddWebApi();

var app = builder.Build();

app.UseEndpoints(endpoints =>
{
    endpoints.Get("/health", async ctx => await ctx.Response.WriteAsync("OK"));
});

app.Run();
```

### Web API Options

Configure in `appsettings.json`:

```json
{
  "webApi": {
    "bindRequestFromRoute": true
  }
}
```

## Key Features

### 1. Fluent Endpoint Definition

```csharp
app.UseEndpoints(endpoints =>
{
    // Simple GET endpoint
    endpoints.Get("/api/users", async ctx =>
    {
        var users = await userService.GetAllAsync();
        await ctx.Response.WriteAsJsonAsync(users);
    });

    // GET with route parameters
    endpoints.Get<GetUserRequest>("/api/users/{id:int}", async (request, ctx) =>
    {
        var user = await userService.GetAsync(request.Id);
        if (user == null)
        {
            ctx.Response.StatusCode = 404;
            return;
        }

        await ctx.Response.WriteAsJsonAsync(user);
    });

    // POST with request body
    endpoints.Post<CreateUserRequest>("/api/users", async (request, ctx) =>
    {
        var user = await userService.CreateAsync(request);
        ctx.Response.StatusCode = 201;
        ctx.Response.Headers["Location"] = $"/api/users/{user.Id}";
        await ctx.Response.WriteAsJsonAsync(user);
    });
});
```

### 2. Request Models

```csharp
public class GetUserRequest
{
    public int Id { get; set; }
}

public class CreateUserRequest
{
    [Required]
    public string Name { get; set; }

    [Required]
    [EmailAddress]
    public string Email { get; set; }

    public string Role { get; set; } = "User";
}

public class UpdateUserRequest
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
}
```

### 3. Authentication and Authorization

```csharp
app.UseEndpoints(endpoints =>
{
    // Requires authentication
    endpoints.Get("/api/profile",
        auth: true,
        context: async ctx =>
        {
            var userId = ctx.User.Identity.Name;
            var profile = await userService.GetProfileAsync(userId);
            await ctx.Response.WriteAsJsonAsync(profile);
        });

    // Requires specific role
    endpoints.Delete<DeleteUserRequest>("/api/users/{id:int}",
        auth: true,
        roles: "admin",
        context: async (request, ctx) =>
        {
            await userService.DeleteAsync(request.Id);
            ctx.Response.StatusCode = 204;
        });

    // Requires policy
    endpoints.Post<CreateOrderRequest>("/api/orders",
        auth: true,
        policies: new[] { "CanCreateOrders" },
        context: async (request, ctx) =>
        {
            var order = await orderService.CreateAsync(request);
            await ctx.Response.WriteAsJsonAsync(order);
        });
});
```

### 4. All HTTP Methods Support

```csharp
app.UseEndpoints(endpoints =>
{
    // GET
    endpoints.Get<GetRequest>("/api/resource/{id}", async (req, ctx) => { });

    // POST
    endpoints.Post<CreateRequest>("/api/resource", async (req, ctx) => { });

    // PUT
    endpoints.Put<UpdateRequest>("/api/resource/{id}", async (req, ctx) => { });

    // PATCH
    endpoints.Patch<PatchRequest>("/api/resource/{id}", async (req, ctx) => { });

    // DELETE
    endpoints.Delete<DeleteRequest>("/api/resource/{id}", async (req, ctx) => { });

    // HEAD
    endpoints.Head("/api/resource/{id}", async ctx => { });
});
```

### 5. Custom Endpoint Configuration

```csharp
app.UseEndpoints(endpoints =>
{
    endpoints.Get<GetProductsRequest>("/api/products",
        context: async (request, ctx) =>
        {
            var products = await productService.GetAsync(request);
            await ctx.Response.WriteAsJsonAsync(products);
        },
        endpoint: e => e
            .WithName("GetProducts")
            .WithDisplayName("Get Products")
            .WithTags("Products")
            .Produces<ProductDto[]>(200)
            .ProducesProblem(400)
        );
});
```

## Advanced Features

### 1. Request Validation

```csharp
public class CreateProductRequest
{
    [Required]
    [StringLength(100, MinimumLength = 3)]
    public string Name { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Price { get; set; }

    [StringLength(500)]
    public string Description { get; set; }
}

// Automatic validation with detailed error responses
endpoints.Post<CreateProductRequest>("/api/products", async (request, ctx) =>
{
    // Request is automatically validated
    var product = await productService.CreateAsync(request);
    await ctx.Response.WriteAsJsonAsync(product);
});
```

### 2. Response Helpers

```csharp
app.UseEndpoints(endpoints =>
{
    endpoints.Get<GetUserRequest>("/api/users/{id}", async (request, ctx) =>
    {
        var user = await userService.GetAsync(request.Id);

        if (user == null)
        {
            await ctx.Response.NotFound();
            return;
        }

        await ctx.Response.Ok(user);
    });

    endpoints.Post<CreateUserRequest>("/api/users", async (request, ctx) =>
    {
        try
        {
            var user = await userService.CreateAsync(request);
            await ctx.Response.Created($"/api/users/{user.Id}", user);
        }
        catch (ValidationException ex)
        {
            await ctx.Response.BadRequest(ex.Message);
        }
        catch (ConflictException ex)
        {
            await ctx.Response.Conflict(ex.Message);
        }
    });
});
```

### 3. Middleware Integration

```csharp
app.UseEndpoints(endpoints =>
{
    // Custom middleware for specific endpoints
    endpoints.Get("/api/admin/stats",
        auth: true,
        roles: "admin",
        context: async ctx =>
        {
            var stats = await adminService.GetStatsAsync();
            await ctx.Response.WriteAsJsonAsync(stats);
        },
        endpoint: e => e.RequireAuthorization("AdminOnly")
    );
},
useAuthorization: true,
middleware: app =>
{
    app.UseMiddleware<RequestLoggingMiddleware>();
    app.UseMiddleware<RateLimitingMiddleware>();
});
```

## API Reference

### IEndpointsBuilder Methods

All endpoint methods follow this pattern:

```csharp
IEndpointsBuilder Method<T>(string path,
    Func<T, HttpContext, Task> context = null,
    Action<IEndpointConventionBuilder> endpoint = null,
    bool auth = false,
    string roles = null,
    params string[] policies)
```

**Parameters:**
- `path` - Route pattern with parameter placeholders
- `context` - Handler function for the endpoint
- `endpoint` - Endpoint configuration (metadata, OpenAPI, etc.)
- `auth` - Whether authentication is required
- `roles` - Comma-separated list of required roles
- `policies` - Array of required authorization policies

### Extension Methods

#### AddWebApi()
```csharp
public static IConveyBuilder AddWebApi(this IConveyBuilder builder,
    Action<IMvcCoreBuilder> configureMvc = null,
    IJsonSerializer jsonSerializer = null,
    string sectionName = "webApi")
```

Registers Web API services with the DI container.

#### UseEndpoints()
```csharp
public static IApplicationBuilder UseEndpoints(this IApplicationBuilder app,
    Action<IEndpointsBuilder> build,
    bool useAuthorization = true,
    Action<IApplicationBuilder> middleware = null)
```

Configures the application pipeline with endpoint routing.

## Complete Example

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddWebApi()
    .AddJwt();

// Register services
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IProductService, ProductService>();

var app = builder.Build();

// Configure endpoints
app.UseEndpoints(endpoints =>
{
    // Public endpoints
    endpoints.Get("/health", async ctx => await ctx.Response.WriteAsync("Healthy"));

    endpoints.Post<LoginRequest>("/api/auth/login", async (request, ctx) =>
    {
        var token = await authService.LoginAsync(request);
        await ctx.Response.WriteAsJsonAsync(new { token });
    });

    // Protected user endpoints
    endpoints.Get("/api/users/me",
        auth: true,
        context: async ctx =>
        {
            var userId = ctx.User.Identity.Name;
            var user = await userService.GetAsync(userId);
            await ctx.Response.WriteAsJsonAsync(user);
        });

    endpoints.Put<UpdateProfileRequest>("/api/users/me",
        auth: true,
        context: async (request, ctx) =>
        {
            var userId = ctx.User.Identity.Name;
            await userService.UpdateAsync(userId, request);
            ctx.Response.StatusCode = 204;
        });

    // Admin endpoints
    endpoints.Get("/api/admin/users",
        auth: true,
        roles: "admin",
        context: async ctx =>
        {
            var users = await userService.GetAllAsync();
            await ctx.Response.WriteAsJsonAsync(users);
        });

    endpoints.Delete<DeleteUserRequest>("/api/admin/users/{id:int}",
        auth: true,
        roles: "admin",
        context: async (request, ctx) =>
        {
            await userService.DeleteAsync(request.Id);
            ctx.Response.StatusCode = 204;
        });

    // Product endpoints with validation
    endpoints.Post<CreateProductRequest>("/api/products",
        auth: true,
        policies: new[] { "CanCreateProducts" },
        context: async (request, ctx) =>
        {
            var product = await productService.CreateAsync(request);
            ctx.Response.StatusCode = 201;
            ctx.Response.Headers["Location"] = $"/api/products/{product.Id}";
            await ctx.Response.WriteAsJsonAsync(product);
        },
        endpoint: e => e
            .WithName("CreateProduct")
            .WithTags("Products")
            .Accepts<CreateProductRequest>("application/json")
            .Produces<ProductDto>(201)
            .ProducesProblem(400)
            .ProducesProblem(401)
            .ProducesProblem(403)
    );
});

app.Run();

// Request/Response models
public class LoginRequest
{
    [Required]
    public string Email { get; set; }

    [Required]
    public string Password { get; set; }
}

public class UpdateProfileRequest
{
    public string Name { get; set; }
    public string Email { get; set; }
}

public class CreateProductRequest
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Price { get; set; }

    [StringLength(500)]
    public string Description { get; set; }

    public string Category { get; set; }
}

public class DeleteUserRequest
{
    public int Id { get; set; }
}
```

## Best Practices

1. **Use request models** - Always define typed request models for parameter binding
2. **Implement validation** - Use data annotations for request validation
3. **Handle errors gracefully** - Provide meaningful error responses
4. **Use appropriate status codes** - Follow HTTP status code conventions
5. **Secure endpoints** - Apply authentication and authorization as needed
6. **Document endpoints** - Use endpoint configuration for OpenAPI documentation
7. **Keep handlers focused** - Delegate business logic to services
8. **Use consistent patterns** - Establish consistent request/response patterns

## Integration with Other Convey Packages

### With CQRS
```csharp
endpoints.Post<CreateOrderCommand>("/api/orders",
    auth: true,
    context: async (command, ctx) =>
    {
        await commandDispatcher.SendAsync(command);
        ctx.Response.StatusCode = 202;
    });

endpoints.Get<GetOrderQuery>("/api/orders/{id:guid}",
    context: async (query, ctx) =>
    {
        var order = await queryDispatcher.QueryAsync(query);
        await ctx.Response.WriteAsJsonAsync(order);
    });
```

### With Swagger
```csharp
builder.Services.AddConvey()
    .AddWebApi()
    .AddSwaggerDocs();

app.UseEndpoints(endpoints =>
{
    endpoints.Get<GetUsersQuery>("/api/users",
        endpoint: e => e
            .WithOpenApi(op => op.Summary = "Get all users")
            .Produces<UserDto[]>(200)
    );
});
```

## Troubleshooting

### Common Issues

1. **Request binding failures**
   - Ensure request models have parameterless constructors
   - Check route parameter names match model properties
   - Verify JSON property naming conventions

2. **Authentication not working**
   - Ensure `UseAuthentication()` is called before `UseEndpoints()`
   - Check JWT configuration and token format
   - Verify role and policy requirements

3. **Validation errors**
   - Check data annotation attributes
   - Ensure ModelState validation is enabled
   - Verify request content type is `application/json`

4. **Routing conflicts**
   - Check route patterns for conflicts
   - Use constraints for route parameters
   - Order specific routes before generic ones

