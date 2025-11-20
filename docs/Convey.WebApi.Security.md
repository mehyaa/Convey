# Convey.WebApi.Security

Web API security extensions providing authentication, authorization, CORS configuration, rate limiting, and security headers for ASP.NET Core applications with integrated security policies and middleware.

## Installation

```bash
dotnet add package Convey.WebApi.Security
```

## Overview

Convey.WebApi.Security provides:
- **Authentication integration** - JWT, API key, and certificate authentication
- **Authorization policies** - Role-based and policy-based authorization
- **CORS configuration** - Cross-Origin Resource Sharing setup
- **Rate limiting** - Request throttling and rate limiting
- **Security headers** - HSTS, CSP, and other security headers
- **Input validation** - Request sanitization and validation
- **API versioning security** - Version-specific security policies
- **Audit logging** - Security event logging and monitoring

## Configuration

### Basic Security Setup

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddWebApi()
    .AddWebApiSecurity() // Enables security features
    .AddJwt(); // JWT authentication

var app = builder.Build();

app.UseAuthentication()
   .UseAuthorization()
   .UseSecurityHeaders()
   .UseCors()
   .UseRateLimiting();

app.Run();
```

### Comprehensive Security Configuration

```csharp
var builder = WebApplication.CreateBuilder(args);

// Security configuration
builder.Services.AddConvey()
    .AddWebApi()
    .AddWebApiSecurity(options =>
    {
        options.EnableCors = true;
        options.EnableRateLimiting = true;
        options.EnableSecurityHeaders = true;
        options.EnableInputValidation = true;
        options.EnableAuditLogging = true;
    })
    .AddJwt()
    .AddSecurityHeaders(headers =>
    {
        headers.AddDefaultSecurePolicy();
        headers.AddContentSecurityPolicy("default-src 'self'; script-src 'self' 'unsafe-inline'");
        headers.AddStrictTransportSecurity(maxAge: TimeSpan.FromDays(365));
    })
    .AddCors(cors =>
    {
        cors.AddPolicy("ApiPolicy", policy =>
        {
            policy.WithOrigins("https://localhost:3000", "https://api.example.com")
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        });
    })
    .AddRateLimiting(options =>
    {
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: context.User?.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString(),
                factory: partition => new FixedWindowRateLimiterOptions
                {
                    AutoReplenishment = true,
                    PermitLimit = 100,
                    Window = TimeSpan.FromMinutes(1)
                }));
    });

var app = builder.Build();

// Security middleware pipeline
app.UseSecurityHeaders();
app.UseRateLimiter();
app.UseCors("ApiPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.Run();
```

## Key Features

### 1. Authentication Integration

Multiple authentication schemes:

```csharp
// JWT Authentication
public class JwtAuthenticationSetup
{
    public static void Configure(IServiceCollection services, IConfiguration configuration)
    {
        services.AddConvey()
            .AddJwt(jwt =>
            {
                jwt.SecretKey = configuration["JWT:SecretKey"];
                jwt.Issuer = configuration["JWT:Issuer"];
                jwt.Audience = configuration["JWT:Audience"];
                jwt.ExpiryMinutes = 60;
                jwt.ValidateIssuer = true;
                jwt.ValidateAudience = true;
                jwt.ValidateLifetime = true;
                jwt.ValidateIssuerSigningKey = true;
                jwt.ClockSkew = TimeSpan.Zero;
            });
    }
}

// API Key Authentication
public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationSchemeOptions>
{
    private readonly IApiKeyService _apiKeyService;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock,
        IApiKeyService apiKeyService)
        : base(options, logger, encoder, clock)
    {
        _apiKeyService = apiKeyService;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("X-API-Key"))
        {
            return AuthenticateResult.Fail("API Key header not found");
        }

        var apiKey = Request.Headers["X-API-Key"].FirstOrDefault();
        if (string.IsNullOrEmpty(apiKey))
        {
            return AuthenticateResult.Fail("API Key is empty");
        }

        var apiKeyInfo = await _apiKeyService.ValidateApiKeyAsync(apiKey);
        if (apiKeyInfo == null)
        {
            return AuthenticateResult.Fail("Invalid API Key");
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, apiKeyInfo.Name),
            new Claim(ClaimTypes.NameIdentifier, apiKeyInfo.Id.ToString()),
            new Claim("ApiKeyId", apiKeyInfo.Id.ToString()),
            new Claim("Scope", apiKeyInfo.Scope)
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }
}

// Certificate Authentication
public class CertificateAuthenticationSetup
{
    public static void Configure(IServiceCollection services)
    {
        services.AddAuthentication(CertificateAuthenticationDefaults.AuthenticationScheme)
            .AddCertificate(options =>
            {
                options.AllowedCertificateTypes = CertificateTypes.All;
                options.RevocationMode = X509RevocationMode.NoCheck;
                options.Events = new CertificateAuthenticationEvents
                {
                    OnCertificateValidated = context =>
                    {
                        var claims = new[]
                        {
                            new Claim(ClaimTypes.NameIdentifier, context.ClientCertificate.Subject),
                            new Claim(ClaimTypes.Name, context.ClientCertificate.GetNameInfo(X509NameType.SimpleName, false)),
                            new Claim("thumbprint", context.ClientCertificate.Thumbprint)
                        };

                        context.Principal = new ClaimsPrincipal(new ClaimsIdentity(claims, context.Scheme.Name));
                        context.Success();
                        return Task.CompletedTask;
                    }
                };
            });
    }
}

// Multi-scheme authentication
services.AddAuthentication()
    .AddJwtBearer("JWT", options => { /* JWT config */ })
    .AddScheme<ApiKeyAuthenticationSchemeOptions, ApiKeyAuthenticationHandler>("ApiKey", options => { })
    .AddCertificate("Certificate", options => { /* Certificate config */ });

// Authentication endpoints
app.UseEndpoints(endpoints =>
{
    // JWT protected endpoint
    endpoints.Get("/api/users/profile", async (HttpContext ctx) =>
    {
        var user = ctx.User;
        await ctx.Response.WriteAsJsonAsync(new
        {
            Id = user.FindFirst(ClaimTypes.NameIdentifier)?.Value,
            Name = user.FindFirst(ClaimTypes.Name)?.Value,
            Email = user.FindFirst(ClaimTypes.Email)?.Value,
            Roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray()
        });
    }).RequireAuthorization("JWT");

    // API Key protected endpoint
    endpoints.Get("/api/data/export", async (HttpContext ctx) =>
    {
        var apiKeyId = ctx.User.FindFirst("ApiKeyId")?.Value;
        var scope = ctx.User.FindFirst("Scope")?.Value;

        // Export logic based on API key scope
        await ctx.Response.WriteAsJsonAsync(new { Message = "Data exported", ApiKeyId = apiKeyId });
    }).RequireAuthorization("ApiKey");

    // Certificate protected endpoint
    endpoints.Get("/api/admin/system-info", async (HttpContext ctx) =>
    {
        var thumbprint = ctx.User.FindFirst("thumbprint")?.Value;

        // Admin operations
        await ctx.Response.WriteAsJsonAsync(new { SystemInfo = "...", CertThumbprint = thumbprint });
    }).RequireAuthorization("Certificate");
});
```

### 2. Authorization Policies

Role-based and policy-based authorization:

```csharp
// Authorization policies
services.AddAuthorization(options =>
{
    // Role-based policies
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("ManagerOrAdmin", policy => policy.RequireRole("Manager", "Admin"));

    // Claims-based policies
    options.AddPolicy("CanEditUsers", policy =>
        policy.RequireClaim("permission", "users.edit"));

    options.AddPolicy("CanViewReports", policy =>
        policy.RequireClaim("permission", "reports.view"));

    // Custom requirement policies
    options.AddPolicy("MinimumAge", policy =>
        policy.Requirements.Add(new MinimumAgeRequirement(18)));

    options.AddPolicy("SameUser", policy =>
        policy.Requirements.Add(new SameUserRequirement()));

    // Resource-based policies
    options.AddPolicy("DocumentOwner", policy =>
        policy.Requirements.Add(new DocumentOwnerRequirement()));

    // Time-based policies
    options.AddPolicy("BusinessHours", policy =>
        policy.Requirements.Add(new BusinessHoursRequirement()));

    // IP-based policies
    options.AddPolicy("InternalNetwork", policy =>
        policy.Requirements.Add(new IpAddressRequirement("192.168.1.0/24")));
});

// Custom authorization requirements
public class MinimumAgeRequirement : IAuthorizationRequirement
{
    public int MinimumAge { get; }

    public MinimumAgeRequirement(int minimumAge)
    {
        MinimumAge = minimumAge;
    }
}

public class MinimumAgeHandler : AuthorizationHandler<MinimumAgeRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        MinimumAgeRequirement requirement)
    {
        var dobClaim = context.User.FindFirst("dob");
        if (dobClaim != null && DateTime.TryParse(dobClaim.Value, out var dob))
        {
            var age = DateTime.Today.Year - dob.Year;
            if (dob.Date > DateTime.Today.AddYears(-age)) age--;

            if (age >= requirement.MinimumAge)
            {
                context.Succeed(requirement);
            }
        }

        return Task.CompletedTask;
    }
}

// Resource-based authorization
public class DocumentOwnerRequirement : IAuthorizationRequirement { }

public class DocumentOwnerHandler : AuthorizationHandler<DocumentOwnerRequirement, Document>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        DocumentOwnerRequirement requirement,
        Document resource)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (resource.OwnerId == userId || context.User.IsInRole("Admin"))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

// Authorization endpoints
app.UseEndpoints(endpoints =>
{
    // Role-based authorization
    endpoints.Get("/api/admin/users", async (HttpContext ctx) =>
    {
        // Admin-only endpoint
    }).RequireAuthorization("AdminOnly");

    // Claims-based authorization
    endpoints.Post("/api/users/{id}", async (HttpContext ctx) =>
    {
        // Requires edit permission
    }).RequireAuthorization("CanEditUsers");

    // Resource-based authorization
    endpoints.Get("/api/documents/{id}", async (Guid id, HttpContext ctx, IAuthorizationService authService) =>
    {
        var document = await GetDocumentAsync(id);
        var authResult = await authService.AuthorizeAsync(ctx.User, document, "DocumentOwner");

        if (!authResult.Succeeded)
        {
            ctx.Response.StatusCode = 403;
            return;
        }

        await ctx.Response.WriteAsJsonAsync(document);
    });

    // Multiple policy authorization
    endpoints.Delete("/api/users/{id}", async (HttpContext ctx) =>
    {
        // Requires both admin role and business hours
    }).RequireAuthorization("AdminOnly", "BusinessHours");
});

// Register authorization handlers
services.AddScoped<IAuthorizationHandler, MinimumAgeHandler>();
services.AddScoped<IAuthorizationHandler, DocumentOwnerHandler>();
```

### 3. CORS Configuration

Cross-Origin Resource Sharing setup:

```csharp
// CORS configuration
services.AddCors(options =>
{
    // Development policy (permissive)
    options.AddPolicy("Development", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });

    // Production policy (restrictive)
    options.AddPolicy("Production", policy =>
    {
        policy.WithOrigins(
                "https://app.example.com",
                "https://admin.example.com",
                "https://mobile.example.com")
              .WithMethods("GET", "POST", "PUT", "DELETE")
              .WithHeaders("Content-Type", "Authorization", "X-Requested-With")
              .AllowCredentials()
              .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
    });

    // API-specific policy
    options.AddPolicy("ApiConsumers", policy =>
    {
        policy.WithOrigins("https://partner1.com", "https://partner2.com")
              .WithMethods("GET", "POST")
              .WithHeaders("Content-Type", "Authorization", "X-API-Key")
              .WithExposedHeaders("X-Total-Count", "X-Page-Count")
              .SetIsOriginAllowedToReturnTrue(); // Dynamic origin validation
    });

    // Mobile app policy
    options.AddPolicy("MobileApps", policy =>
    {
        policy.WithOrigins("capacitor://localhost", "ionic://localhost")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Dynamic CORS policy
services.AddSingleton<ICorsPolicyProvider, DynamicCorsPolicyProvider>();

public class DynamicCorsPolicyProvider : ICorsPolicyProvider
{
    private readonly ILogger<DynamicCorsPolicyProvider> _logger;
    private readonly IConfiguration _configuration;

    public DynamicCorsPolicyProvider(ILogger<DynamicCorsPolicyProvider> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public Task<CorsPolicy> GetPolicyAsync(HttpContext context, string policyName)
    {
        var origin = context.Request.Headers["Origin"].ToString();

        // Get allowed origins from configuration or database
        var allowedOrigins = _configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();

        var policy = new CorsPolicyBuilder()
            .WithOrigins(allowedOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials()
            .Build();

        _logger.LogInformation("CORS policy applied for origin: {Origin}", origin);

        return Task.FromResult(policy);
    }
}

// Conditional CORS usage
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseCors("Development");
}
else
{
    app.UseCors("Production");
}
```

### 4. Rate Limiting

Request throttling and rate limiting:

```csharp
// Rate limiting configuration
services.AddRateLimiter(options =>
{
    // Global rate limiter
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var userId = context.User?.Identity?.Name;
        var clientId = context.Request.Headers["X-Client-Id"].FirstOrDefault();
        var ipAddress = context.Connection.RemoteIpAddress?.ToString();

        var partitionKey = userId ?? clientId ?? ipAddress ?? "anonymous";

        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            AutoReplenishment = true,
            PermitLimit = 100,
            Window = TimeSpan.FromMinutes(1)
        });
    });

    // Per-endpoint policies
    options.AddPolicy("AuthPolicy", context =>
    {
        var userId = context.User?.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString();

        return RateLimitPartition.GetTokenBucketLimiter(userId, _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = 10,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 5,
            ReplenishmentPeriod = TimeSpan.FromSeconds(10),
            TokensPerPeriod = 2,
            AutoReplenishment = true
        });
    });

    options.AddPolicy("ApiPolicy", context =>
    {
        var apiKey = context.Request.Headers["X-API-Key"].FirstOrDefault();

        return RateLimitPartition.GetSlidingWindowLimiter(apiKey ?? "unknown", _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = 1000,
            Window = TimeSpan.FromHours(1),
            SegmentsPerWindow = 12,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 10
        });
    });

    options.AddPolicy("UploadPolicy", context =>
    {
        var userId = context.User?.Identity?.Name ?? "anonymous";

        return RateLimitPartition.GetConcurrencyLimiter(userId, _ => new ConcurrencyLimiterOptions
        {
            PermitLimit = 3,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 5
        });
    });

    // Custom rejection response
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = 429;

        var response = new
        {
            Error = "Rate limit exceeded",
            RetryAfter = context.Lease.GetAllMetadata().FirstOrDefault(m => m.Key == "RETRY_AFTER")?.Value,
            Limit = context.Lease.GetAllMetadata().FirstOrDefault(m => m.Key == "LIMIT")?.Value
        };

        await context.HttpContext.Response.WriteAsJsonAsync(response, cancellationToken: token);
    };
});

// Rate limited endpoints
app.UseEndpoints(endpoints =>
{
    // Authentication endpoints with stricter limits
    endpoints.Post("/api/auth/login", async (HttpContext ctx) =>
    {
        // Login logic
    }).RequireRateLimiting("AuthPolicy");

    // API endpoints with standard limits
    endpoints.Get("/api/data", async (HttpContext ctx) =>
    {
        // Data retrieval
    }).RequireRateLimiting("ApiPolicy");

    // File upload with concurrency limits
    endpoints.Post("/api/upload", async (HttpContext ctx) =>
    {
        // File upload logic
    }).RequireRateLimiting("UploadPolicy");

    // Global rate limiting (uses global limiter)
    endpoints.Get("/api/public/status", async (HttpContext ctx) =>
    {
        await ctx.Response.WriteAsJsonAsync(new { Status = "OK" });
    });
});

app.UseRateLimiter();
```

### 5. Security Headers

Implement security headers for protection:

```csharp
// Security headers configuration
services.AddSecurityHeaders(options =>
{
    // Strict Transport Security
    options.AddStrictTransportSecurity(maxAge: TimeSpan.FromDays(365), includeSubdomains: true);

    // Content Security Policy
    options.AddContentSecurityPolicy(builder =>
    {
        builder.DefaultSources.Self()
               .ScriptSources.Self().UnsafeInline().From("https://cdn.jsdelivr.net")
               .StyleSources.Self().UnsafeInline().From("https://fonts.googleapis.com")
               .FontSources.Self().From("https://fonts.gstatic.com")
               .ImageSources.Self().Data().From("https:")
               .ConnectSources.Self().From("https://api.example.com")
               .FrameAncestors.None()
               .BaseUris.Self();
    });

    // X-Frame-Options
    options.AddFrameOptions(FrameOptionsPolicy.Deny);

    // X-Content-Type-Options
    options.AddContentTypeOptions();

    // Referrer Policy
    options.AddReferrerPolicy(ReferrerPolicy.StrictOriginWhenCrossOrigin);

    // Permissions Policy
    options.AddPermissionsPolicy(builder =>
    {
        builder.Camera.None()
               .Microphone.None()
               .Geolocation.Self()
               .Payment.None()
               .Usb.None();
    });

    // Custom headers
    options.AddCustomHeader("X-API-Version", "v1.0");
    options.AddCustomHeader("X-Powered-By", "Convey Framework");

    // Remove server header
    options.RemoveServerHeader = true;
});

// Custom security headers middleware
public class CustomSecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CustomSecurityHeadersMiddleware> _logger;

    public CustomSecurityHeadersMiddleware(RequestDelegate next, ILogger<CustomSecurityHeadersMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Add security headers based on request
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;

            // Content Security Policy for different endpoints
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                headers["Content-Security-Policy"] = "default-src 'none'";
            }
            else if (context.Request.Path.StartsWithSegments("/admin"))
            {
                headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self' 'unsafe-inline'";
            }

            // API-specific headers
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                headers["X-Content-Type-Options"] = "nosniff";
                headers["X-Frame-Options"] = "DENY";
                headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
                headers["Pragma"] = "no-cache";
                headers["Expires"] = "0";
            }

            // Remove sensitive headers
            headers.Remove("Server");
            headers.Remove("X-Powered-By");
            headers.Remove("X-AspNet-Version");

            return Task.CompletedTask;
        });

        await _next(context);
    }
}

// Register middleware
app.UseMiddleware<CustomSecurityHeadersMiddleware>();
app.UseSecurityHeaders();
```

### 6. Input Validation and Sanitization

Request validation and sanitization:

```csharp
// Input validation middleware
public class InputValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<InputValidationMiddleware> _logger;

    public InputValidationMiddleware(RequestDelegate next, ILogger<InputValidationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Validate request size
        if (context.Request.ContentLength > 10 * 1024 * 1024) // 10MB limit
        {
            context.Response.StatusCode = 413;
            await context.Response.WriteAsync("Request too large");
            return;
        }

        // Validate content type for POST/PUT requests
        if (context.Request.Method is "POST" or "PUT")
        {
            var contentType = context.Request.ContentType;
            if (!IsAllowedContentType(contentType))
            {
                context.Response.StatusCode = 415;
                await context.Response.WriteAsync("Unsupported media type");
                return;
            }
        }

        // Validate headers
        foreach (var header in context.Request.Headers)
        {
            if (ContainsSuspiciousContent(header.Value))
            {
                _logger.LogWarning("Suspicious header detected: {HeaderName}", header.Key);
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("Invalid request");
                return;
            }
        }

        // Validate query parameters
        foreach (var param in context.Request.Query)
        {
            if (ContainsSuspiciousContent(param.Value))
            {
                _logger.LogWarning("Suspicious query parameter detected: {ParamName}", param.Key);
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("Invalid request");
                return;
            }
        }

        await _next(context);
    }

    private bool IsAllowedContentType(string contentType)
    {
        var allowedTypes = new[]
        {
            "application/json",
            "application/xml",
            "text/plain",
            "multipart/form-data",
            "application/x-www-form-urlencoded"
        };

        return allowedTypes.Any(type => contentType?.StartsWith(type) == true);
    }

    private bool ContainsSuspiciousContent(string content)
    {
        var suspiciousPatterns = new[]
        {
            @"<script",
            @"javascript:",
            @"vbscript:",
            @"onload=",
            @"onerror=",
            @"eval\(",
            @"document\.cookie",
            @"window\.location"
        };

        return suspiciousPatterns.Any(pattern =>
            Regex.IsMatch(content, pattern, RegexOptions.IgnoreCase));
    }
}

// Request sanitization
public static class RequestSanitizer
{
    public static string SanitizeInput(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        // Remove potentially dangerous characters
        input = input.Replace("<", "&lt;")
                    .Replace(">", "&gt;")
                    .Replace("\"", "&quot;")
                    .Replace("'", "&#x27;")
                    .Replace("/", "&#x2F;");

        // Remove script tags
        input = Regex.Replace(input, @"<script[^>]*>.*?</script>", "", RegexOptions.IgnoreCase);

        // Remove event handlers
        input = Regex.Replace(input, @"on\w+\s*=", "", RegexOptions.IgnoreCase);

        return input;
    }

    public static T SanitizeObject<T>(T obj) where T : class
    {
        if (obj == null) return obj;

        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && p.PropertyType == typeof(string));

        foreach (var property in properties)
        {
            var value = property.GetValue(obj) as string;
            if (!string.IsNullOrEmpty(value))
            {
                property.SetValue(obj, SanitizeInput(value));
            }
        }

        return obj;
    }
}

// Usage in endpoints
app.UseEndpoints(endpoints =>
{
    endpoints.Post<CreateUserCommand>("/api/users", async (command, ctx) =>
    {
        // Sanitize input
        RequestSanitizer.SanitizeObject(command);

        var commandDispatcher = ctx.RequestServices.GetRequiredService<ICommandDispatcher>();
        await commandDispatcher.SendAsync(command);

        ctx.Response.StatusCode = 201;
    });
});

app.UseMiddleware<InputValidationMiddleware>();
```

## Configuration Options

### Security Options

```csharp
public class WebApiSecurityOptions
{
    public bool EnableCors { get; set; } = true;
    public bool EnableRateLimiting { get; set; } = true;
    public bool EnableSecurityHeaders { get; set; } = true;
    public bool EnableInputValidation { get; set; } = true;
    public bool EnableAuditLogging { get; set; } = true;
    public string[] AllowedContentTypes { get; set; } = new[] { "application/json" };
    public long MaxRequestSize { get; set; } = 10 * 1024 * 1024; // 10MB
}
```

## API Reference

### Extension Methods

```csharp
public static class ConveyExtensions
{
    public static IConveyBuilder AddWebApiSecurity(this IConveyBuilder builder, Action<WebApiSecurityOptions> configure = null);
    public static IConveyBuilder AddSecurityHeaders(this IConveyBuilder builder, Action<SecurityHeadersOptions> configure = null);
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app);
    public static IApplicationBuilder UseInputValidation(this IApplicationBuilder app);
}
```

## Best Practices

1. **Defense in depth** - Implement multiple layers of security
2. **Principle of least privilege** - Grant minimum required permissions
3. **Input validation** - Validate and sanitize all inputs
4. **Secure headers** - Implement comprehensive security headers
5. **Rate limiting** - Protect against abuse and DoS attacks
6. **Audit logging** - Log security-relevant events
7. **Regular updates** - Keep security packages updated
8. **Security testing** - Regularly test security implementations

## Troubleshooting

### Common Issues

1. **CORS errors**
   - Check origin configuration in CORS policy
   - Verify preflight request handling
   - Ensure credentials are properly configured

2. **Rate limiting not working**
   - Verify rate limiter is registered and configured
   - Check partition key generation
   - Ensure middleware order is correct

3. **Authentication failures**
   - Check JWT token validation settings
   - Verify certificate configuration
   - Ensure proper claim mapping

4. **Authorization denials**
   - Verify policy requirements
   - Check user roles and claims
   - Ensure handlers are registered
