---
layout: default
title: Convey.HTTP.RestEase
parent: HTTP & API
---
# Convey.HTTP.RestEase

HTTP client integration using RestEase library providing declarative HTTP API clients, automatic serialization, and seamless integration with Convey's service discovery and resilience patterns.

## Installation

```bash
dotnet add package Convey.HTTP.RestEase
```

## Overview

Convey.HTTP.RestEase provides:
- **RestEase integration** - Declarative HTTP API clients with attributes
- **Service discovery** - Automatic endpoint resolution via Consul
- **Retry policies** - Configurable retry strategies with Polly
- **Circuit breakers** - Fault tolerance and resilience patterns
- **Request/Response interceptors** - Middleware for HTTP requests
- **Correlation tracking** - Automatic correlation ID propagation
- **Authentication** - JWT and API key authentication support
- **Serialization** - JSON and XML serialization options

## Configuration

### Basic RestEase Setup

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddConsul()
    .AddHttpClient()
    .AddRestEaseClient<IUserService>("user-service");

var app = builder.Build();
app.Run();
```

### Advanced RestEase Configuration

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddConsul()
    .AddHttpClient()
    .AddRestEaseClient<IUserService>("user-service", client =>
    {
        client.LoadBalancer = "fabio";
        client.Services = new[] { "user-service" };
        client.Scheme = "https";
        client.Port = 443;
        client.RequestId = true;
        client.RemoveCharsetFromContentType = true;
        client.Serializers = new SerializersOptions
        {
            JsonSerializer = JsonSerializer.SystemTextJson
        };
    })
    .AddRestEaseClient<IOrderService>("order-service", client =>
    {
        client.Services = new[] { "order-service", "orders-api" };
        client.RequestId = true;
        client.Headers = new Dictionary<string, string>
        {
            ["X-API-Version"] = "v1",
            ["Accept"] = "application/json"
        };
    })
    .AddRestEaseClient<IPaymentService>("payment-service", "https://payment-api.example.com");

var app = builder.Build();
app.Run();
```

## Key Features

### 1. Declarative API Clients

Define HTTP APIs using interfaces and attributes:

```csharp
// User service API client
[Header("User-Agent", "MyApp/1.0")]
[Header("Accept", "application/json")]
public interface IUserService
{
    [Get("api/users/{id}")]
    Task<UserDto> GetUserAsync([Path] Guid id, CancellationToken cancellationToken = default);

    [Get("api/users")]
    Task<PagedResult<UserDto>> GetUsersAsync([Query] GetUsersQuery query, CancellationToken cancellationToken = default);

    [Post("api/users")]
    Task<UserDto> CreateUserAsync([Body] CreateUserRequest request, CancellationToken cancellationToken = default);

    [Put("api/users/{id}")]
    Task UpdateUserAsync([Path] Guid id, [Body] UpdateUserRequest request, CancellationToken cancellationToken = default);

    [Delete("api/users/{id}")]
    Task DeleteUserAsync([Path] Guid id, CancellationToken cancellationToken = default);

    [Get("api/users/{id}/orders")]
    Task<IEnumerable<OrderDto>> GetUserOrdersAsync([Path] Guid id, [Query] DateTime? from = null, [Query] DateTime? to = null);

    [Post("api/users/{id}/activate")]
    Task ActivateUserAsync([Path] Guid id);

    [Post("api/users/{id}/deactivate")]
    Task DeactivateUserAsync([Path] Guid id);

    [Get("api/users/search")]
    Task<IEnumerable<UserDto>> SearchUsersAsync([Query] string query, [Query] int limit = 10);
}

// Order service API client
[Header("Authorization", "Bearer")]
public interface IOrderService
{
    [Get("api/orders/{id}")]
    Task<OrderDto> GetOrderAsync([Path] Guid id);

    [Get("api/orders")]
    Task<PagedResult<OrderDto>> GetOrdersAsync([Query] GetOrdersQuery query);

    [Post("api/orders")]
    [Header("Content-Type", "application/json")]
    Task<OrderDto> CreateOrderAsync([Body] CreateOrderRequest request);

    [Put("api/orders/{id}/status")]
    Task UpdateOrderStatusAsync([Path] Guid id, [Body] UpdateOrderStatusRequest request);

    [Post("api/orders/{id}/cancel")]
    Task CancelOrderAsync([Path] Guid id, [Body] CancelOrderRequest request);

    [Get("api/orders/{id}/items")]
    Task<IEnumerable<OrderItemDto>> GetOrderItemsAsync([Path] Guid id);

    [Post("api/orders/{id}/items")]
    Task AddOrderItemAsync([Path] Guid id, [Body] AddOrderItemRequest request);

    [Delete("api/orders/{id}/items/{itemId}")]
    Task RemoveOrderItemAsync([Path] Guid id, [Path] Guid itemId);

    [Get("api/orders/reports/daily")]
    Task<DailyOrderReport> GetDailyReportAsync([Query] DateTime date);
}

// Payment service API client with custom serialization
public interface IPaymentService
{
    [Post("api/payments/process")]
    [Header("Content-Type", "application/json")]
    Task<PaymentResult> ProcessPaymentAsync([Body] ProcessPaymentRequest request);

    [Get("api/payments/{id}")]
    Task<PaymentDto> GetPaymentAsync([Path] string id);

    [Post("api/payments/{id}/refund")]
    Task<RefundResult> RefundPaymentAsync([Path] string id, [Body] RefundRequest request);

    [Get("api/payments/methods")]
    Task<IEnumerable<PaymentMethodDto>> GetPaymentMethodsAsync();

    [Post("api/payments/webhook")]
    [Header("Content-Type", "application/x-www-form-urlencoded")]
    Task HandleWebhookAsync([Body(BodySerializationMethod.UrlEncoded)] Dictionary<string, string> data);
}

// External API client with authentication
[Header("X-API-Key", "your-api-key")]
public interface IExternalApiService
{
    [Get("api/v1/data")]
    Task<ExternalDataResponse> GetDataAsync([Query] string filter);

    [Post("api/v1/notifications")]
    Task SendNotificationAsync([Body] NotificationRequest request);

    [Get("api/v1/status")]
    [AllowAnyStatusCode]
    Task<Response<StatusResponse>> GetStatusAsync();
}

// File upload service
public interface IFileService
{
    [Post("api/files/upload")]
    [Header("Content-Type", "multipart/form-data")]
    Task<FileUploadResult> UploadFileAsync([Body] MultipartFormDataContent content);

    [Get("api/files/{id}")]
    Task<Stream> DownloadFileAsync([Path] string id);

    [Delete("api/files/{id}")]
    Task DeleteFileAsync([Path] string id);

    [Get("api/files/{id}/metadata")]
    Task<FileMetadata> GetFileMetadataAsync([Path] string id);
}
```

### 2. Service Integration

Use RestEase clients in your services:

```csharp
// User management service using RestEase client
public class UserManagementService
{
    private readonly IUserService _userService;
    private readonly IOrderService _orderService;
    private readonly ILogger<UserManagementService> _logger;

    public UserManagementService(
        IUserService userService,
        IOrderService orderService,
        ILogger<UserManagementService> logger)
    {
        _userService = userService;
        _orderService = orderService;
        _logger = logger;
    }

    public async Task<UserDto> GetUserWithOrdersAsync(Guid userId)
    {
        try
        {
            // Get user details
            var user = await _userService.GetUserAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found", userId);
                return null;
            }

            // Get user's orders
            var orders = await _userService.GetUserOrdersAsync(userId);
            user.Orders = orders.ToList();

            _logger.LogDebug("Retrieved user {UserId} with {OrderCount} orders", userId, orders.Count());
            return user;
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("404"))
        {
            _logger.LogWarning("User {UserId} not found", userId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user {UserId}", userId);
            throw;
        }
    }

    public async Task<UserDto> CreateUserAccountAsync(CreateUserRequest request)
    {
        try
        {
            // Check if user already exists
            var existingUsers = await _userService.SearchUsersAsync(request.Email, 1);
            if (existingUsers.Any())
            {
                throw new InvalidOperationException($"User with email {request.Email} already exists");
            }

            // Create user
            var user = await _userService.CreateUserAsync(request);

            // Activate user account
            await _userService.ActivateUserAsync(user.Id);

            _logger.LogInformation("Created and activated user account: {UserId}", user.Id);
            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user account for email {Email}", request.Email);
            throw;
        }
    }

    public async Task<PagedResult<UserDto>> SearchUsersAsync(string searchTerm, int page = 1, int pageSize = 20)
    {
        try
        {
            var query = new GetUsersQuery
            {
                SearchTerm = searchTerm,
                Page = page,
                PageSize = pageSize
            };

            var result = await _userService.GetUsersAsync(query);

            _logger.LogDebug("Search for '{SearchTerm}' returned {Count} users", searchTerm, result.Items.Count());
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching users with term '{SearchTerm}'", searchTerm);
            throw;
        }
    }
}

// Order processing service
public class OrderProcessingService
{
    private readonly IOrderService _orderService;
    private readonly IPaymentService _paymentService;
    private readonly IUserService _userService;
    private readonly ILogger<OrderProcessingService> _logger;

    public OrderProcessingService(
        IOrderService orderService,
        IPaymentService paymentService,
        IUserService userService,
        ILogger<OrderProcessingService> logger)
    {
        _orderService = orderService;
        _paymentService = paymentService;
        _userService = userService;
        _logger = logger;
    }

    public async Task<OrderDto> ProcessOrderAsync(CreateOrderRequest request)
    {
        try
        {
            // Validate user
            var user = await _userService.GetUserAsync(request.UserId);
            if (user == null)
            {
                throw new InvalidOperationException($"User {request.UserId} not found");
            }

            // Create order
            var order = await _orderService.CreateOrderAsync(request);
            _logger.LogInformation("Created order {OrderId} for user {UserId}", order.Id, request.UserId);

            // Process payment
            var paymentRequest = new ProcessPaymentRequest
            {
                OrderId = order.Id,
                Amount = order.TotalAmount,
                Currency = "USD",
                PaymentMethod = request.PaymentMethod
            };

            var paymentResult = await _paymentService.ProcessPaymentAsync(paymentRequest);

            if (paymentResult.Success)
            {
                // Update order status
                await _orderService.UpdateOrderStatusAsync(order.Id, new UpdateOrderStatusRequest
                {
                    Status = OrderStatus.Paid,
                    PaymentId = paymentResult.PaymentId
                });

                _logger.LogInformation("Successfully processed payment for order {OrderId}", order.Id);
            }
            else
            {
                // Cancel order on payment failure
                await _orderService.CancelOrderAsync(order.Id, new CancelOrderRequest
                {
                    Reason = $"Payment failed: {paymentResult.ErrorMessage}"
                });

                _logger.LogWarning("Payment failed for order {OrderId}: {Error}", order.Id, paymentResult.ErrorMessage);
                throw new InvalidOperationException($"Payment failed: {paymentResult.ErrorMessage}");
            }

            return order;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing order for user {UserId}", request.UserId);
            throw;
        }
    }

    public async Task<DailyOrderReport> GenerateDailyReportAsync(DateTime date)
    {
        try
        {
            var report = await _orderService.GetDailyReportAsync(date);
            _logger.LogDebug("Generated daily report for {Date}: {OrderCount} orders",
                date.ToString("yyyy-MM-dd"), report.TotalOrders);

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating daily report for {Date}", date);
            throw;
        }
    }
}

// File management service
public class FileManagementService
{
    private readonly IFileService _fileService;
    private readonly ILogger<FileManagementService> _logger;

    public FileManagementService(IFileService fileService, ILogger<FileManagementService> logger)
    {
        _fileService = fileService;
        _logger = logger;
    }

    public async Task<FileUploadResult> UploadFileAsync(IFormFile file, string category = null)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            using var fileContent = new StreamContent(file.OpenReadStream());

            fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
            content.Add(fileContent, "file", file.FileName);

            if (!string.IsNullOrEmpty(category))
            {
                content.Add(new StringContent(category), "category");
            }

            var result = await _fileService.UploadFileAsync(content);

            _logger.LogInformation("Uploaded file {FileName} with ID {FileId}", file.FileName, result.FileId);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file {FileName}", file.FileName);
            throw;
        }
    }

    public async Task<Stream> DownloadFileAsync(string fileId)
    {
        try
        {
            var stream = await _fileService.DownloadFileAsync(fileId);
            _logger.LogDebug("Downloaded file {FileId}", fileId);
            return stream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading file {FileId}", fileId);
            throw;
        }
    }

    public async Task<FileMetadata> GetFileInfoAsync(string fileId)
    {
        try
        {
            var metadata = await _fileService.GetFileMetadataAsync(fileId);
            return metadata;
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("404"))
        {
            _logger.LogWarning("File {FileId} not found", fileId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting file metadata for {FileId}", fileId);
            throw;
        }
    }
}
```

### 3. Authentication and Headers

Configure authentication and custom headers:

```csharp
// JWT authentication interceptor
public class JwtAuthenticationInterceptor : IRequestInterceptor
{
    private readonly ITokenProvider _tokenProvider;
    private readonly ILogger<JwtAuthenticationInterceptor> _logger;

    public JwtAuthenticationInterceptor(ITokenProvider tokenProvider, ILogger<JwtAuthenticationInterceptor> logger)
    {
        _tokenProvider = tokenProvider;
        _logger = logger;
    }

    public async Task InterceptAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            var token = await _tokenProvider.GetTokenAsync();
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                _logger.LogDebug("Added JWT token to request {Method} {Uri}", request.Method, request.RequestUri);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to add JWT token to request");
        }
    }
}

// Correlation ID interceptor
public class CorrelationIdInterceptor : IRequestInterceptor
{
    private readonly ICorrelationIdProvider _correlationIdProvider;

    public CorrelationIdInterceptor(ICorrelationIdProvider correlationIdProvider)
    {
        _correlationIdProvider = correlationIdProvider;
    }

    public Task InterceptAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var correlationId = _correlationIdProvider.Get();
        if (!string.IsNullOrEmpty(correlationId))
        {
            request.Headers.Add("X-Correlation-ID", correlationId);
        }

        return Task.CompletedTask;
    }
}

// Custom response interceptor
public class LoggingResponseInterceptor : IResponseInterceptor
{
    private readonly ILogger<LoggingResponseInterceptor> _logger;

    public LoggingResponseInterceptor(ILogger<LoggingResponseInterceptor> logger)
    {
        _logger = logger;
    }

    public async Task<HttpResponseMessage> InterceptAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var duration = response.Headers.GetValues("X-Response-Time").FirstOrDefault();

        _logger.LogInformation("HTTP {Method} {Uri} responded with {StatusCode} in {Duration}ms",
            response.RequestMessage?.Method,
            response.RequestMessage?.RequestUri,
            (int)response.StatusCode,
            duration);

        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("HTTP request failed: {StatusCode} {Content}", response.StatusCode, content);
        }

        return response;
    }
}

// Register interceptors
builder.Services.AddConvey()
    .AddHttpClient()
    .AddRestEaseClient<IUserService>("user-service")
    .AddTransient<IRequestInterceptor, JwtAuthenticationInterceptor>()
    .AddTransient<IRequestInterceptor, CorrelationIdInterceptor>()
    .AddTransient<IResponseInterceptor, LoggingResponseInterceptor>();
```

### 4. Error Handling and Resilience

Implement retry policies and circuit breakers:

```csharp
// Resilient HTTP client service
public class ResilientUserService
{
    private readonly IUserService _userService;
    private readonly ILogger<ResilientUserService> _logger;

    public ResilientUserService(IUserService userService, ILogger<ResilientUserService> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    public async Task<UserDto> GetUserWithRetryAsync(Guid userId)
    {
        var retryPolicy = Policy
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning("Retry {RetryCount} for GetUser({UserId}) in {Delay}ms",
                        retryCount, userId, timespan.TotalMilliseconds);
                });

        return await retryPolicy.ExecuteAsync(async () =>
        {
            try
            {
                var user = await _userService.GetUserAsync(userId);
                _logger.LogDebug("Successfully retrieved user {UserId}", userId);
                return user;
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("404"))
            {
                _logger.LogWarning("User {UserId} not found", userId);
                return null; // Don't retry for 404
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user {UserId}, attempt will be retried", userId);
                throw;
            }
        });
    }

    public async Task<bool> TryCreateUserAsync(CreateUserRequest request, int maxRetries = 3)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await _userService.CreateUserAsync(request);
                _logger.LogInformation("Successfully created user {Email} on attempt {Attempt}", request.Email, attempt);
                return true;
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("409"))
            {
                _logger.LogWarning("User {Email} already exists", request.Email);
                return false; // Don't retry for conflict
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create user {Email} on attempt {Attempt}/{MaxRetries}",
                    request.Email, attempt, maxRetries);

                if (attempt == maxRetries)
                {
                    _logger.LogError("Failed to create user {Email} after {MaxRetries} attempts", request.Email, maxRetries);
                    return false;
                }

                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
            }
        }

        return false;
    }
}

// Circuit breaker implementation
public class CircuitBreakerService
{
    private readonly IOrderService _orderService;
    private readonly ICircuitBreaker _circuitBreaker;
    private readonly ILogger<CircuitBreakerService> _logger;

    public CircuitBreakerService(IOrderService orderService, ILogger<CircuitBreakerService> logger)
    {
        _orderService = orderService;
        _logger = logger;

        _circuitBreaker = Policy
            .Handle<HttpRequestException>()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 3,
                durationOfBreak: TimeSpan.FromMinutes(1),
                onBreak: (exception, duration) =>
                {
                    _logger.LogWarning("Circuit breaker opened for {Duration}ms due to: {Exception}",
                        duration.TotalMilliseconds, exception.Message);
                },
                onReset: () =>
                {
                    _logger.LogInformation("Circuit breaker reset");
                },
                onHalfOpen: () =>
                {
                    _logger.LogInformation("Circuit breaker half-open");
                });
    }

    public async Task<OrderDto> GetOrderSafelyAsync(Guid orderId)
    {
        try
        {
            return await _circuitBreaker.ExecuteAsync(async () =>
            {
                return await _orderService.GetOrderAsync(orderId);
            });
        }
        catch (CircuitBreakerOpenException)
        {
            _logger.LogWarning("Circuit breaker is open, cannot retrieve order {OrderId}", orderId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving order {OrderId}", orderId);
            throw;
        }
    }
}
```

### 5. Custom Serialization

Configure custom serialization options:

```csharp
// Custom JSON serializer options
builder.Services.AddConvey()
    .AddHttpClient()
    .AddRestEaseClient<IApiService>("api-service", client =>
    {
        client.Serializers = new SerializersOptions
        {
            JsonSerializer = JsonSerializer.SystemTextJson,
            JsonSerializerOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Converters = { new JsonStringEnumConverter() }
            }
        };
    });

// Custom date time converter
public class CustomDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return DateTime.ParseExact(reader.GetString(), "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString("yyyy-MM-dd HH:mm:ss"));
    }
}

// XML serialization support
[SerializationMethod(BodySerializationMethod.Xml)]
public interface IXmlApiService
{
    [Post("api/data")]
    [Header("Content-Type", "application/xml")]
    Task<XmlResponse> PostDataAsync([Body] XmlRequest request);

    [Get("api/data/{id}")]
    [Header("Accept", "application/xml")]
    Task<XmlDataResponse> GetDataAsync([Path] string id);
}
```

## Configuration Options

### RestEase Client Options

```csharp
public class RestEaseClientOptions
{
    public string LoadBalancer { get; set; }
    public string[] Services { get; set; }
    public string Scheme { get; set; } = "http";
    public string Host { get; set; }
    public int Port { get; set; }
    public bool RequestId { get; set; } = true;
    public bool RemoveCharsetFromContentType { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new();
    public SerializersOptions Serializers { get; set; } = new();
}

public class SerializersOptions
{
    public JsonSerializer JsonSerializer { get; set; } = JsonSerializer.SystemTextJson;
    public JsonSerializerOptions JsonSerializerOptions { get; set; }
}

public enum JsonSerializer
{
    SystemTextJson,
    NewtonsoftJson
}
```

## API Reference

### Extension Methods

```csharp
public static class ConveyExtensions
{
    public static IConveyBuilder AddRestEaseClient<T>(this IConveyBuilder builder, string serviceName) where T : class;
    public static IConveyBuilder AddRestEaseClient<T>(this IConveyBuilder builder, string serviceName, Action<RestEaseClientOptions> configure) where T : class;
    public static IConveyBuilder AddRestEaseClient<T>(this IConveyBuilder builder, string serviceName, string baseUrl) where T : class;
}
```

## Best Practices

1. **Use service discovery** - Leverage Consul for dynamic endpoint resolution
2. **Implement retry policies** - Handle transient failures gracefully
3. **Add correlation tracking** - Include correlation IDs for distributed tracing
4. **Configure timeouts** - Set appropriate request timeouts
5. **Handle authentication** - Implement proper token management
6. **Use typed clients** - Define strongly-typed API interfaces
7. **Log HTTP activities** - Monitor request/response patterns
8. **Implement circuit breakers** - Protect against cascading failures

## Troubleshooting

### Common Issues

1. **Service discovery failures**
   - Check Consul connectivity and configuration
   - Verify service registration and health checks
   - Ensure correct service names

2. **Authentication errors**
   - Verify token provider implementation
   - Check JWT token validity and expiration
   - Ensure proper header configuration

3. **Serialization problems**
   - Check JSON serialization settings
   - Verify model property naming conventions
   - Ensure proper content type headers

4. **Network timeouts**
   - Adjust HTTP client timeout settings
   - Implement appropriate retry policies
   - Check network connectivity and latency

