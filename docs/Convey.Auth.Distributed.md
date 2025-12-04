---
layout: default
title: Convey.Auth.Distributed
parent: Authentication & Security
---
# Convey.Auth.Distributed

Distributed access token service that provides token blacklisting functionality using distributed caching. This package extends the basic JWT authentication provided by `Convey.Auth` with distributed token revocation capabilities.

## Installation

```bash
dotnet add package Convey.Auth.Distributed
```

## Overview

Convey.Auth.Distributed provides:
- **Distributed token blacklisting** - Revoke access tokens across multiple service instances
- **Distributed cache integration** - Uses `IDistributedCache` for token storage
- **Token validation** - Check if tokens are blacklisted before processing requests
- **Seamless replacement** - Drop-in replacement for the in-memory token service

## Configuration

### Basic Setup

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddJwt() // Configure JWT authentication first
    .AddDistributedAccessTokenValidator(); // Enable distributed token blacklisting

// Add distributed cache (required dependency)
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = "localhost:6379";
});

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.UseAccessTokenValidator(); // Enable token validation middleware

app.Run();
```

### JWT Configuration

Configure JWT settings in `appsettings.json`:

```json
{
  "jwt": {
    "issuer": "your-issuer",
    "audience": "your-audience",
    "issuerSigningKey": "your-secret-key",
    "expiryMinutes": 60,
    "validateIssuer": true,
    "validateAudience": true,
    "validateLifetime": true,
    "validateIssuerSigningKey": true
  }
}
```

### With Other Distributed Cache Providers

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddJwt()
    .AddDistributedAccessTokenValidator();

// Using SQL Server distributed cache
builder.Services.AddDistributedSqlServerCache(options =>
{
    options.ConnectionString = "your-connection-string";
    options.SchemaName = "dbo";
    options.TableName = "TestCache";
});

// Or using memory cache (for single instance)
builder.Services.AddDistributedMemoryCache();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.UseAccessTokenValidator();

app.Run();
```

## Key Features

### 1. Distributed Token Blacklisting

The main feature is replacing the in-memory token blacklisting with distributed caching:

```csharp
// The service automatically replaces InMemoryAccessTokenService
public class DistributedAccessTokenService : IAccessTokenService
{
    private readonly IDistributedCache _cache;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly TimeSpan _expires;

    public DistributedAccessTokenService(IDistributedCache cache,
        IHttpContextAccessor httpContextAccessor,
        JwtOptions jwtOptions)
    {
        _cache = cache;
        _httpContextAccessor = httpContextAccessor;
        _expires = jwtOptions.Expiry ?? TimeSpan.FromMinutes(jwtOptions.ExpiryMinutes);
    }

    public Task<bool> IsCurrentActiveToken()
        => IsActiveAsync(GetCurrentAsync());

    public Task DeactivateCurrentAsync()
        => DeactivateAsync(GetCurrentAsync());

    public async Task<bool> IsActiveAsync(string token)
        => string.IsNullOrWhiteSpace(await _cache.GetStringAsync(GetKey(token)));

    public Task DeactivateAsync(string token)
        => _cache.SetStringAsync(GetKey(token), "revoked",
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _expires
            });

    private string GetCurrentAsync()
    {
        var authorizationHeader = _httpContextAccessor
            .HttpContext.Request.Headers["authorization"];

        return authorizationHeader == StringValues.Empty
            ? string.Empty
            : authorizationHeader.Single().Split(' ').Last();
    }

    private static string GetKey(string token) => $"blacklisted-tokens:{token}";
}
```

### 2. Usage in Controllers

Use the `IAccessTokenService` to manage token lifecycle:

```csharp
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAccessTokenService _tokenService;

    public AuthController(IAccessTokenService tokenService)
    {
        _tokenService = tokenService;
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _tokenService.DeactivateCurrentAsync();
        return Ok(new { message = "Successfully logged out" });
    }

    [HttpPost("logout-all")]
    [Authorize]
    public async Task<IActionResult> LogoutFromAllDevices([FromBody] LogoutAllRequest request)
    {
        // Deactivate specific tokens (you would need to track user tokens)
        foreach (var token in request.Tokens)
        {
            await _tokenService.DeactivateAsync(token);
        }

        return Ok(new { message = "Successfully logged out from all devices" });
    }

    [HttpGet("check-token")]
    public async Task<IActionResult> CheckToken([FromQuery] string token)
    {
        var isActive = await _tokenService.IsActiveAsync(token);
        return Ok(new { token, isActive });
    }
}

public class LogoutAllRequest
{
    public IEnumerable<string> Tokens { get; set; }
}
```

### 3. Middleware Integration

The token validation middleware automatically checks blacklisted tokens:

```csharp
// This middleware is provided by Convey.Auth
public class AccessTokenValidatorMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IAccessTokenService _accessTokenService;

    public AccessTokenValidatorMiddleware(RequestDelegate next, IAccessTokenService accessTokenService)
    {
        _next = next;
        _accessTokenService = accessTokenService;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.HasValue ? context.Request.Path.Value : string.Empty;

        if (context.User.Identity.IsAuthenticated)
        {
            var isActiveToken = await _accessTokenService.IsCurrentActiveToken();
            if (!isActiveToken)
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Unauthorized");
                return;
            }
        }

        await _next(context);
    }
}
```

## API Reference

### IAccessTokenService Interface

```csharp
public interface IAccessTokenService
{
    Task<bool> IsCurrentActiveToken();
    Task DeactivateCurrentAsync();
    Task<bool> IsActiveAsync(string token);
    Task DeactivateAsync(string token);
}
```

### Extension Methods

```csharp
public static class Extensions
{
    public static IConveyBuilder AddDistributedAccessTokenValidator(this IConveyBuilder builder);
}
```

## Dependencies

This package requires:
- `Convey.Auth` - For JWT authentication and base interfaces
- `Microsoft.Extensions.Caching.Abstractions` - For `IDistributedCache` interface
- A distributed cache implementation (Redis, SQL Server, etc.)

## Best Practices

1. **Choose appropriate cache provider** - Use Redis for multi-instance scenarios, SQL Server for existing SQL infrastructure
2. **Configure cache expiration** - Ensure cache entries expire at the same time as JWT tokens
3. **Monitor cache performance** - Track cache hit rates and response times
4. **Handle cache failures gracefully** - Consider fallback strategies when cache is unavailable
5. **Secure cache communication** - Use encrypted connections to cache stores
6. **Regular cache cleanup** - Implement strategies to clean up expired entries

## Troubleshooting

### Common Issues

1. **Cache connection failures**
   - Verify cache server is running and accessible
   - Check connection strings and authentication
   - Ensure network connectivity

2. **Token still valid after logout**
   - Verify `UseAccessTokenValidator` middleware is registered
   - Check middleware order in pipeline
   - Confirm distributed cache is working

3. **Performance issues**
   - Optimize cache configuration
   - Consider connection pooling
   - Monitor cache server resources

4. **Tokens not being blacklisted**
   - Verify `AddDistributedAccessTokenValidator` is called
   - Check if JWT options are properly configured
   - Ensure token expiration times match cache expiration

