---
layout: default
title: Convey.Auth
parent: Authentication & Security
---
# Convey.Auth

JWT (JSON Web Token) authentication library with support for secret keys and X.509 certificates. Provides comprehensive authentication and authorization capabilities for microservices.

## Installation

```bash
dotnet add package Convey.Auth
```

## Overview

Convey.Auth provides:
- **JWT token generation and validation** - Complete JWT lifecycle management
- **X.509 certificate support** - Certificate-based token signing and validation
- **Flexible authentication options** - Support for various authentication schemes
- **Access token management** - In-memory token storage and validation
- **Authorization attributes** - Declarative authorization for controllers and actions
- **Middleware integration** - Seamless ASP.NET Core pipeline integration

## Configuration

### Basic Setup

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddJwt();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.Run();
```

### JWT Options

Configure JWT settings in `appsettings.json`:

```json
{
  "jwt": {
    "issuer": "my-service",
    "issuerSigningKey": "my-super-secret-key-that-is-at-least-256-bits-long",
    "audience": "my-service-users",
    "expiry": "01:00:00",
    "validateIssuer": true,
    "validateAudience": true,
    "validateLifetime": true,
    "validateIssuerSigningKey": true,
    "algorithm": "HS256"
  }
}
```

### Certificate-based Configuration

```json
{
  "jwt": {
    "issuer": "my-service",
    "audience": "my-service-users",
    "certificate": {
      "location": "certs/jwt-signing.pfx",
      "password": "certificate-password"
    },
    "algorithm": "RS256"
  }
}
```

## Key Features

### 1. JWT Token Generation

```csharp
public class AuthController : ControllerBase
{
    private readonly IJwtHandler _jwtHandler;

    public AuthController(IJwtHandler jwtHandler)
    {
        _jwtHandler = jwtHandler;
    }

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        // Validate user credentials
        var user = ValidateUser(request.Email, request.Password);
        if (user == null)
        {
            return Unauthorized();
        }

        // Create JWT token
        var claims = new Dictionary<string, IEnumerable<string>>
        {
            { "email", new[] { user.Email } },
            { "permissions", user.Permissions }
        };

        var token = _jwtHandler.CreateToken(
            userId: user.Id.ToString(),
            role: user.Role,
            audience: "my-app",
            claims: claims
        );

        return Ok(new { token = token.AccessToken });
    }
}
```

### 2. Authorization Attributes

```csharp
[Route("api/[controller]")]
[ApiController]
public class UsersController : ControllerBase
{
    [HttpGet]
    [JwtAuth] // Requires valid JWT token
    public IActionResult GetUsers()
    {
        return Ok(GetAllUsers());
    }

    [HttpGet("{id}")]
    [JwtAuth(Role = "admin")] // Requires admin role
    public IActionResult GetUser(int id)
    {
        return Ok(GetUserById(id));
    }

    [HttpPost]
    [Auth(Policy = "CanCreateUsers")] // Custom policy
    public IActionResult CreateUser([FromBody] CreateUserRequest request)
    {
        return Ok(CreateNewUser(request));
    }
}
```

### 3. Access Token Validation

```csharp
public class TokenService
{
    private readonly IAccessTokenService _tokenService;

    public TokenService(IAccessTokenService tokenService)
    {
        _tokenService = tokenService;
    }

    public async Task<bool> IsTokenValidAsync(string token)
    {
        return await _tokenService.IsActiveAsync(token);
    }

    public async Task RevokeTokenAsync(string token)
    {
        await _tokenService.DeactivateAsync(token);
    }
}
```

### 4. Token Payload Extraction

```csharp
public class UserService
{
    private readonly IJwtHandler _jwtHandler;

    public UserService(IJwtHandler jwtHandler)
    {
        _jwtHandler = jwtHandler;
    }

    public UserInfo GetCurrentUser(string token)
    {
        var payload = _jwtHandler.GetTokenPayload(token);

        return new UserInfo
        {
            UserId = payload.Subject,
            Email = payload.Claims["email"].FirstOrDefault(),
            Roles = payload.Claims["role"] ?? new string[0]
        };
    }
}
```

## Advanced Configuration

### Multiple Issuers
```json
{
  "jwt": {
    "validIssuers": ["service-a", "service-b", "identity-provider"],
    "validAudiences": ["api-gateway", "user-service", "order-service"]
  }
}
```

### Custom Authentication Type
```json
{
  "jwt": {
    "authenticationType": "CustomJWT",
    "nameClaimType": "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name",
    "roleClaimType": "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
  }
}
```

### Disable Authentication (Development)
```json
{
  "jwt": {
    "authenticationDisabled": true,
    "allowAnonymousEndpoints": [
      "/health",
      "/metrics",
      "/swagger"
    ]
  }
}
```

## API Reference

### IJwtHandler

```csharp
public interface IJwtHandler
{
    JsonWebToken CreateToken(string userId, string role = null, string audience = null,
        IDictionary<string, IEnumerable<string>> claims = null);
    JsonWebTokenPayload GetTokenPayload(string accessToken);
}
```

#### CreateToken()
Creates a new JWT token with specified claims.

**Parameters:**
- `userId` - Unique user identifier (becomes 'sub' claim)
- `role` - User role (becomes 'role' claim)
- `audience` - Token audience (becomes 'aud' claim)
- `claims` - Additional custom claims

**Returns:** `JsonWebToken` containing the access token and metadata

#### GetTokenPayload()
Extracts and validates token payload without full authentication.

**Parameters:**
- `accessToken` - JWT token string

**Returns:** `JsonWebTokenPayload` with decoded claims

### IAccessTokenService

```csharp
public interface IAccessTokenService
{
    Task<bool> IsActiveAsync(string token);
    Task ActivateAsync(string token, string userId);
    Task<bool> DeactivateAsync(string token, string userId);
    Task<bool> DeactivateAsync(string token);
    Task DeactivateCurrentAsync(string userId);
    Task<bool> IsActiveAsync(string token, string userId);
}
```

Manages token lifecycle and revocation.

### Authorization Attributes

#### [JwtAuth]
```csharp
[JwtAuth] // Requires valid JWT
[JwtAuth(Role = "admin")] // Requires specific role
[JwtAuth(Policy = "CustomPolicy")] // Requires policy
```

#### [Auth]
```csharp
[Auth] // Basic authentication required
[Auth(Policy = "PolicyName")] // Policy-based authorization
```

## Token Structure

### JsonWebToken
```csharp
public class JsonWebToken
{
    public string AccessToken { get; set; }
    public long Expires { get; set; }
    public string Id { get; set; }
    public string Role { get; set; }
    public IDictionary<string, IEnumerable<string>> Claims { get; set; }
}
```

### JsonWebTokenPayload
```csharp
public class JsonWebTokenPayload
{
    public string Subject { get; set; }
    public string Role { get; set; }
    public string Audience { get; set; }
    public string Issuer { get; set; }
    public IDictionary<string, IEnumerable<string>> Claims { get; set; }
    public long Expires { get; set; }
}
```

## Usage Examples

### Complete Authentication Flow

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddJwt();

builder.Services.AddControllers();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

// AuthController.cs
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IJwtHandler _jwtHandler;
    private readonly IUserService _userService;

    public AuthController(IJwtHandler jwtHandler, IUserService userService)
    {
        _jwtHandler = jwtHandler;
        _userService = userService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> LoginAsync([FromBody] LoginRequest request)
    {
        var user = await _userService.AuthenticateAsync(request.Email, request.Password);
        if (user == null)
        {
            return Unauthorized();
        }

        var token = _jwtHandler.CreateToken(
            userId: user.Id.ToString(),
            role: user.Role,
            claims: new Dictionary<string, IEnumerable<string>>
            {
                { "email", new[] { user.Email } },
                { "name", new[] { user.Name } }
            }
        );

        return Ok(new LoginResponse
        {
            Token = token.AccessToken,
            ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(token.Expires),
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                Name = user.Name,
                Role = user.Role
            }
        });
    }

    [HttpPost("logout")]
    [JwtAuth]
    public async Task<IActionResult> LogoutAsync()
    {
        var token = HttpContext.Request.Headers["Authorization"]
            .ToString().Replace("Bearer ", "");

        await _accessTokenService.DeactivateAsync(token);
        return NoContent();
    }
}

// Protected controller
[ApiController]
[Route("api/[controller]")]
[JwtAuth]
public class ProfileController : ControllerBase
{
    [HttpGet]
    public IActionResult GetProfile()
    {
        var userId = User.Identity.Name;
        var email = User.FindFirst("email")?.Value;

        return Ok(new { UserId = userId, Email = email });
    }

    [HttpPut]
    [JwtAuth(Role = "admin")]
    public IActionResult UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        // Only admins can update profiles
        return Ok();
    }
}
```

### Certificate-based Setup

```csharp
// Certificate configuration
{
  "jwt": {
    "issuer": "my-secure-service",
    "certificate": {
      "location": "certificates/signing-cert.pfx",
      "password": "secure-password"
    },
    "algorithm": "RS256"
  }
}

// Or using raw certificate data
{
  "jwt": {
    "certificate": {
      "rawData": "MIIC...base64-encoded-certificate-data...",
      "password": "certificate-password"
    }
  }
}
```

## Best Practices

1. **Use strong signing keys** - Minimum 256 bits for HMAC, prefer RSA certificates for production
2. **Set appropriate expiration times** - Balance security with user experience
3. **Implement token revocation** - Use `IAccessTokenService` for logout scenarios
4. **Validate all token properties** - Enable issuer, audience, and lifetime validation
5. **Use HTTPS only** - Never transmit tokens over unencrypted connections
6. **Store certificates securely** - Use secure certificate storage in production
7. **Implement refresh tokens** - For long-lived applications, implement token refresh

## Security Considerations

- **Key Management** - Rotate signing keys regularly
- **Token Storage** - Store tokens securely on the client side
- **Claims Validation** - Validate all custom claims in your application
- **Audience Validation** - Ensure tokens are intended for your service
- **HTTPS Enforcement** - Always use HTTPS in production
- **Certificate Security** - Protect certificate private keys

## Troubleshooting

### Common Issues

1. **"Invalid signature" errors**
   - Verify signing key matches between services
   - Check certificate password and location
   - Ensure algorithm matches (HS256 vs RS256)

2. **"Token expired" errors**
   - Check server time synchronization
   - Verify expiration time configuration
   - Consider clock skew settings

3. **Authentication not working**
   - Ensure `UseAuthentication()` is called before `UseAuthorization()`
   - Verify JWT configuration section name
   - Check token format (Bearer prefix)

4. **Claims missing**
   - Verify claims are added during token creation
   - Check claim name mapping configuration
   - Ensure proper deserialization

