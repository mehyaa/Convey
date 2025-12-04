---
layout: default
title: Convey.HTTP
parent: HTTP & API
---
# Convey.HTTP

HTTP client abstractions and integrations providing service discovery-aware HTTP clients, automatic retries, circuit breakers, and comprehensive request/response handling for microservices communication.

## Installation

```bash
dotnet add package Convey.HTTP
```

## Overview

Convey.HTTP provides:
- **Service discovery integration** - HTTP clients that automatically resolve service endpoints
- **Retry policies** - Configurable retry mechanisms with exponential backoff
- **Circuit breaker pattern** - Automatic failover and recovery for failing services
- **Request/response logging** - Comprehensive HTTP request and response logging
- **Correlation ID propagation** - Automatic correlation ID forwarding
- **Performance monitoring** - Built-in metrics and timing collection
- **Content serialization** - JSON and XML serialization support

## Configuration

### Basic Setup

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddHttpClient(); // Enables HTTP client integration

var app = builder.Build();
```

### Advanced Configuration

Configure in `appsettings.json`:

```json
{
  "httpClient": {
    "type": "convey",
    "retries": 3,
    "services": {
      "user-service": {
        "url": "http://user-service:5000",
        "retries": 5,
        "timeout": "00:00:30"
      },
      "order-service": {
        "url": "http://order-service:5001",
        "retries": 3,
        "timeout": "00:01:00"
      }
    }
  }
}
```

### Full Configuration with Service Discovery

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddConsul() // Service discovery
    .AddHttpClient(httpOptions =>
    {
        httpOptions.Type = "convey";
        httpOptions.Retries = 3;
    })
    .AddCorrelationId() // Correlation ID support
    .AddRetryPolicies(); // Retry policies

var app = builder.Build();
```

## Key Features

### 1. Service Discovery-Aware HTTP Clients

HTTP clients that automatically resolve service endpoints:

```csharp
// Service client with automatic service discovery
public class UserServiceClient
{
    private readonly IHttpClient _httpClient;

    public UserServiceClient(IHttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<User> GetUserAsync(Guid userId)
    {
        // Service name automatically resolved via service discovery
        var response = await _httpClient.GetAsync<User>($"user-service/api/users/{userId}");
        return response;
    }

    public async Task<PagedResult<User>> GetUsersAsync(BrowseUsersQuery query)
    {
        var queryString = QueryStringBuilder.Build(query);
        var response = await _httpClient.GetAsync<PagedResult<User>>($"user-service/api/users?{queryString}");
        return response;
    }

    public async Task<Guid> CreateUserAsync(CreateUserRequest request)
    {
        var response = await _httpClient.PostAsync<CreateUserRequest, CreateUserResponse>(
            "user-service/api/users", request);
        return response.Id;
    }

    public async Task UpdateUserAsync(Guid userId, UpdateUserRequest request)
    {
        await _httpClient.PutAsync($"user-service/api/users/{userId}", request);
    }

    public async Task DeleteUserAsync(Guid userId)
    {
        await _httpClient.DeleteAsync($"user-service/api/users/{userId}");
    }
}

// HTTP client interface
public interface IHttpClient
{
    Task<T> GetAsync<T>(string uri);
    Task<TResult> GetAsync<TResult>(string uri, CancellationToken cancellationToken = default);
    Task<HttpResponseMessage> GetAsync(string uri, CancellationToken cancellationToken = default);

    Task<TResult> PostAsync<TRequest, TResult>(string uri, TRequest request, CancellationToken cancellationToken = default);
    Task<HttpResponseMessage> PostAsync<TRequest>(string uri, TRequest request, CancellationToken cancellationToken = default);
    Task<HttpResponseMessage> PostAsync(string uri, HttpContent content = null, CancellationToken cancellationToken = default);

    Task<TResult> PutAsync<TRequest, TResult>(string uri, TRequest request, CancellationToken cancellationToken = default);
    Task<HttpResponseMessage> PutAsync<TRequest>(string uri, TRequest request, CancellationToken cancellationToken = default);
    Task<HttpResponseMessage> PutAsync(string uri, HttpContent content = null, CancellationToken cancellationToken = default);

    Task<HttpResponseMessage> DeleteAsync(string uri, CancellationToken cancellationToken = default);

    Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default);
}
```

### 2. Retry Policies and Circuit Breakers

Implement resilient HTTP communication:

```csharp
// Retry policy configuration
public class RetryPolicyService
{
    private readonly ILogger<RetryPolicyService> _logger;

    public RetryPolicyService(ILogger<RetryPolicyService> logger)
    {
        _logger = logger;
    }

    public IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(int retries = 3)
    {
        return Policy
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .OrResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode && IsTransientFailure(r.StatusCode))
            .WaitAndRetryAsync(
                retryCount: retries,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // Exponential backoff
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    var uri = context.GetValueOrDefault("uri", "unknown");
                    _logger.LogWarning("Retry attempt {RetryCount} for {Uri} in {Delay}ms",
                        retryCount, uri, timespan.TotalMilliseconds);
                });
    }

    public IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy(int failureThreshold = 5, TimeSpan durationOfBreak = default)
    {
        if (durationOfBreak == default)
            durationOfBreak = TimeSpan.FromMinutes(1);

        return Policy
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .OrResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode && IsTransientFailure(r.StatusCode))
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: failureThreshold,
                durationOfBreak: durationOfBreak,
                onBreak: (exception, timespan) =>
                {
                    _logger.LogWarning("Circuit breaker opened for {Duration}ms", timespan.TotalMilliseconds);
                },
                onReset: () =>
                {
                    _logger.LogInformation("Circuit breaker reset");
                });
    }

    private bool IsTransientFailure(HttpStatusCode statusCode)
    {
        return statusCode == HttpStatusCode.RequestTimeout ||
               statusCode == HttpStatusCode.TooManyRequests ||
               (int)statusCode >= 500;
    }
}

// Resilient HTTP client
public class ResilientHttpClient : IHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly IServiceDiscovery _serviceDiscovery;
    private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;
    private readonly IAsyncPolicy<HttpResponseMessage> _circuitBreakerPolicy;
    private readonly ILogger<ResilientHttpClient> _logger;

    public ResilientHttpClient(
        HttpClient httpClient,
        IServiceDiscovery serviceDiscovery,
        RetryPolicyService retryPolicyService,
        ILogger<ResilientHttpClient> logger)
    {
        _httpClient = httpClient;
        _serviceDiscovery = serviceDiscovery;
        _retryPolicy = retryPolicyService.GetRetryPolicy();
        _circuitBreakerPolicy = retryPolicyService.GetCircuitBreakerPolicy();
        _logger = logger;
    }

    public async Task<T> GetAsync<T>(string uri)
    {
        var response = await GetAsync(uri);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(content, JsonSerializerOptions.Default);
    }

    public async Task<HttpResponseMessage> GetAsync(string uri, CancellationToken cancellationToken = default)
    {
        var resolvedUri = await ResolveUriAsync(uri);
        var context = new Context { ["uri"] = resolvedUri };

        var policy = Policy.WrapAsync(_retryPolicy, _circuitBreakerPolicy);

        return await policy.ExecuteAsync(async (ctx) =>
        {
            _logger.LogDebug("Making GET request to {Uri}", resolvedUri);

            var response = await _httpClient.GetAsync(resolvedUri, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("GET request to {Uri} succeeded with status {StatusCode}",
                    resolvedUri, response.StatusCode);
            }
            else
            {
                _logger.LogWarning("GET request to {Uri} failed with status {StatusCode}",
                    resolvedUri, response.StatusCode);
            }

            return response;
        }, context);
    }

    private async Task<string> ResolveUriAsync(string uri)
    {
        // Parse service name from URI (e.g., "user-service/api/users" -> "user-service")
        var segments = uri.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
            return uri;

        var serviceName = segments[0];
        var serviceEndpoint = await _serviceDiscovery.GetServiceUriAsync(serviceName);

        if (serviceEndpoint != null)
        {
            var path = string.Join("/", segments.Skip(1));
            return $"{serviceEndpoint.TrimEnd('/')}/{path}";
        }

        return uri; // Return original if service not found
    }
}
```

### 3. Request/Response Interceptors

Intercept and modify HTTP requests and responses:

```csharp
// Request interceptor for adding correlation IDs
public class CorrelationIdInterceptor : IHttpRequestInterceptor
{
    private readonly ICorrelationIdProvider _correlationIdProvider;

    public CorrelationIdInterceptor(ICorrelationIdProvider correlationIdProvider)
    {
        _correlationIdProvider = correlationIdProvider;
    }

    public Task InterceptAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        var correlationId = _correlationIdProvider.Get();
        if (!string.IsNullOrEmpty(correlationId))
        {
            request.Headers.Add("X-Correlation-ID", correlationId);
        }

        return Task.CompletedTask;
    }
}

// Request interceptor for adding authentication
public class AuthenticationInterceptor : IHttpRequestInterceptor
{
    private readonly ITokenProvider _tokenProvider;

    public AuthenticationInterceptor(ITokenProvider tokenProvider)
    {
        _tokenProvider = tokenProvider;
    }

    public async Task InterceptAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        var token = await _tokenProvider.GetTokenAsync();
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }
}

// Response interceptor for handling errors
public class ErrorHandlingInterceptor : IHttpResponseInterceptor
{
    private readonly ILogger<ErrorHandlingInterceptor> _logger;

    public ErrorHandlingInterceptor(ILogger<ErrorHandlingInterceptor> logger)
    {
        _logger = logger;
    }

    public async Task InterceptAsync(HttpResponseMessage response, CancellationToken cancellationToken = default)
    {
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();

            _logger.LogWarning("HTTP request failed with status {StatusCode}: {Content}",
                response.StatusCode, content);

            // Handle specific error responses
            switch (response.StatusCode)
            {
                case HttpStatusCode.Unauthorized:
                    throw new UnauthorizedAccessException("Authentication required");
                case HttpStatusCode.Forbidden:
                    throw new ForbiddenException("Access denied");
                case HttpStatusCode.NotFound:
                    throw new NotFoundException("Resource not found");
                case HttpStatusCode.BadRequest:
                    var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(content);
                    throw new ValidationException(errorResponse?.Errors ?? new List<string> { "Bad request" });
                default:
                    throw new HttpRequestException($"HTTP request failed with status {response.StatusCode}");
            }
        }
    }
}

// HTTP client with interceptors
public class InterceptedHttpClient : IHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly IEnumerable<IHttpRequestInterceptor> _requestInterceptors;
    private readonly IEnumerable<IHttpResponseInterceptor> _responseInterceptors;

    public InterceptedHttpClient(
        HttpClient httpClient,
        IEnumerable<IHttpRequestInterceptor> requestInterceptors,
        IEnumerable<IHttpResponseInterceptor> responseInterceptors)
    {
        _httpClient = httpClient;
        _requestInterceptors = requestInterceptors;
        _responseInterceptors = responseInterceptors;
    }

    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        // Apply request interceptors
        foreach (var interceptor in _requestInterceptors)
        {
            await interceptor.InterceptAsync(request, cancellationToken);
        }

        // Send request
        var response = await _httpClient.SendAsync(request, cancellationToken);

        // Apply response interceptors
        foreach (var interceptor in _responseInterceptors)
        {
            await interceptor.InterceptAsync(response, cancellationToken);
        }

        return response;
    }
}
```

### 4. Typed HTTP Clients

Create strongly-typed HTTP clients for specific services:

```csharp
// Typed client for user service
public interface IUserServiceClient
{
    Task<User> GetUserAsync(Guid userId);
    Task<PagedResult<User>> BrowseUsersAsync(BrowseUsersQuery query);
    Task<Guid> CreateUserAsync(CreateUserRequest request);
    Task UpdateUserAsync(Guid userId, UpdateUserRequest request);
    Task DeleteUserAsync(Guid userId);
}

public class UserServiceClient : IUserServiceClient
{
    private readonly IHttpClient _httpClient;
    private const string ServiceName = "user-service";

    public UserServiceClient(IHttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<User> GetUserAsync(Guid userId)
    {
        return await _httpClient.GetAsync<User>($"{ServiceName}/api/users/{userId}");
    }

    public async Task<PagedResult<User>> BrowseUsersAsync(BrowseUsersQuery query)
    {
        var queryString = QueryStringBuilder.Build(query);
        return await _httpClient.GetAsync<PagedResult<User>>($"{ServiceName}/api/users?{queryString}");
    }

    public async Task<Guid> CreateUserAsync(CreateUserRequest request)
    {
        var response = await _httpClient.PostAsync<CreateUserRequest, CreateUserResponse>(
            $"{ServiceName}/api/users", request);
        return response.Id;
    }

    public async Task UpdateUserAsync(Guid userId, UpdateUserRequest request)
    {
        await _httpClient.PutAsync($"{ServiceName}/api/users/{userId}", request);
    }

    public async Task DeleteUserAsync(Guid userId)
    {
        await _httpClient.DeleteAsync($"{ServiceName}/api/users/{userId}");
    }
}

// Typed client for order service
public interface IOrderServiceClient
{
    Task<Order> GetOrderAsync(Guid orderId);
    Task<PagedResult<Order>> BrowseOrdersAsync(BrowseOrdersQuery query);
    Task<Guid> CreateOrderAsync(CreateOrderRequest request);
    Task UpdateOrderStatusAsync(Guid orderId, OrderStatus status);
    Task CancelOrderAsync(Guid orderId);
}

public class OrderServiceClient : IOrderServiceClient
{
    private readonly IHttpClient _httpClient;
    private const string ServiceName = "order-service";

    public OrderServiceClient(IHttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<Order> GetOrderAsync(Guid orderId)
    {
        return await _httpClient.GetAsync<Order>($"{ServiceName}/api/orders/{orderId}");
    }

    public async Task<PagedResult<Order>> BrowseOrdersAsync(BrowseOrdersQuery query)
    {
        var queryString = QueryStringBuilder.Build(query);
        return await _httpClient.GetAsync<PagedResult<Order>>($"{ServiceName}/api/orders?{queryString}");
    }

    public async Task<Guid> CreateOrderAsync(CreateOrderRequest request)
    {
        var response = await _httpClient.PostAsync<CreateOrderRequest, CreateOrderResponse>(
            $"{ServiceName}/api/orders", request);
        return response.Id;
    }

    public async Task UpdateOrderStatusAsync(Guid orderId, OrderStatus status)
    {
        await _httpClient.PutAsync($"{ServiceName}/api/orders/{orderId}/status", new { Status = status });
    }

    public async Task CancelOrderAsync(Guid orderId)
    {
        await _httpClient.PostAsync($"{ServiceName}/api/orders/{orderId}/cancel");
    }
}

// Registration
builder.Services.AddHttpClient<IUserServiceClient, UserServiceClient>();
builder.Services.AddHttpClient<IOrderServiceClient, OrderServiceClient>();
```

### 5. Content Serialization and Compression

Handle different content types and compression:

```csharp
// Content serializer
public interface IContentSerializer
{
    Task<string> SerializeAsync<T>(T obj);
    Task<T> DeserializeAsync<T>(string content);
    Task<HttpContent> SerializeToHttpContentAsync<T>(T obj);
    Task<T> DeserializeFromHttpContentAsync<T>(HttpContent content);
}

public class JsonContentSerializer : IContentSerializer
{
    private readonly JsonSerializerOptions _options;

    public JsonContentSerializer(JsonSerializerOptions options = null)
    {
        _options = options ?? new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    public Task<string> SerializeAsync<T>(T obj)
    {
        return Task.FromResult(JsonSerializer.Serialize(obj, _options));
    }

    public Task<T> DeserializeAsync<T>(string content)
    {
        return Task.FromResult(JsonSerializer.Deserialize<T>(content, _options));
    }

    public async Task<HttpContent> SerializeToHttpContentAsync<T>(T obj)
    {
        var json = await SerializeAsync(obj);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    public async Task<T> DeserializeFromHttpContentAsync<T>(HttpContent content)
    {
        var json = await content.ReadAsStringAsync();
        return await DeserializeAsync<T>(json);
    }
}

// Compression handler
public class CompressionHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Add compression headers
        request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
        request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));

        // Compress request content if applicable
        if (request.Content != null && ShouldCompressRequest(request))
        {
            request.Content = await CompressContentAsync(request.Content);
            request.Content.Headers.ContentEncoding.Add("gzip");
        }

        var response = await base.SendAsync(request, cancellationToken);

        // Decompress response if needed
        if (response.Content.Headers.ContentEncoding.Contains("gzip"))
        {
            response.Content = await DecompressContentAsync(response.Content);
        }

        return response;
    }

    private bool ShouldCompressRequest(HttpRequestMessage request)
    {
        return request.Content?.Headers?.ContentLength > 1024; // Compress if > 1KB
    }

    private async Task<HttpContent> CompressContentAsync(HttpContent content)
    {
        var bytes = await content.ReadAsByteArrayAsync();
        var compressedBytes = await CompressAsync(bytes);
        return new ByteArrayContent(compressedBytes);
    }

    private async Task<HttpContent> DecompressContentAsync(HttpContent content)
    {
        var compressedBytes = await content.ReadAsByteArrayAsync();
        var decompressedBytes = await DecompressAsync(compressedBytes);
        return new ByteArrayContent(decompressedBytes);
    }

    private async Task<byte[]> CompressAsync(byte[] data)
    {
        using var output = new MemoryStream();
        using var gzip = new GZipStream(output, CompressionMode.Compress);
        await gzip.WriteAsync(data, 0, data.Length);
        await gzip.FlushAsync();
        return output.ToArray();
    }

    private async Task<byte[]> DecompressAsync(byte[] data)
    {
        using var input = new MemoryStream(data);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        await gzip.CopyToAsync(output);
        return output.ToArray();
    }
}
```

## Advanced Features

### 1. Load Balancing

Implement client-side load balancing:

```csharp
public interface ILoadBalancer
{
    Task<string> GetEndpointAsync(string serviceName);
}

public class RoundRobinLoadBalancer : ILoadBalancer
{
    private readonly IServiceDiscovery _serviceDiscovery;
    private readonly ConcurrentDictionary<string, int> _counters = new();

    public RoundRobinLoadBalancer(IServiceDiscovery serviceDiscovery)
    {
        _serviceDiscovery = serviceDiscovery;
    }

    public async Task<string> GetEndpointAsync(string serviceName)
    {
        var endpoints = await _serviceDiscovery.GetServiceEndpointsAsync(serviceName);

        if (!endpoints.Any())
            throw new ServiceUnavailableException($"No endpoints available for service {serviceName}");

        var counter = _counters.AddOrUpdate(serviceName, 0, (key, value) => (value + 1) % endpoints.Count);
        return endpoints.ElementAt(counter);
    }
}

public class WeightedLoadBalancer : ILoadBalancer
{
    private readonly IServiceDiscovery _serviceDiscovery;
    private readonly Dictionary<string, int> _weights;

    public WeightedLoadBalancer(IServiceDiscovery serviceDiscovery, Dictionary<string, int> weights = null)
    {
        _serviceDiscovery = serviceDiscovery;
        _weights = weights ?? new Dictionary<string, int>();
    }

    public async Task<string> GetEndpointAsync(string serviceName)
    {
        var endpoints = await _serviceDiscovery.GetServiceEndpointsAsync(serviceName);

        if (!endpoints.Any())
            throw new ServiceUnavailableException($"No endpoints available for service {serviceName}");

        // Implement weighted selection logic
        var totalWeight = endpoints.Sum(e => _weights.GetValueOrDefault(e, 1));
        var random = new Random().Next(totalWeight);

        var currentWeight = 0;
        foreach (var endpoint in endpoints)
        {
            currentWeight += _weights.GetValueOrDefault(endpoint, 1);
            if (random < currentWeight)
                return endpoint;
        }

        return endpoints.First(); // Fallback
    }
}
```

### 2. Request/Response Caching

Implement HTTP response caching:

```csharp
public class CachingHandler : DelegatingHandler
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<CachingHandler> _logger;

    public CachingHandler(IMemoryCache cache, ILogger<CachingHandler> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Only cache GET requests
        if (request.Method != HttpMethod.Get)
        {
            return await base.SendAsync(request, cancellationToken);
        }

        var cacheKey = GenerateCacheKey(request);

        // Try to get from cache
        if (_cache.TryGetValue(cacheKey, out CachedResponse cachedResponse))
        {
            _logger.LogDebug("Cache hit for {Uri}", request.RequestUri);
            return CreateResponseFromCache(cachedResponse);
        }

        // Cache miss - make the request
        _logger.LogDebug("Cache miss for {Uri}", request.RequestUri);
        var response = await base.SendAsync(request, cancellationToken);

        // Cache successful responses
        if (response.IsSuccessStatusCode && ShouldCache(request, response))
        {
            await CacheResponseAsync(cacheKey, response);
        }

        return response;
    }

    private string GenerateCacheKey(HttpRequestMessage request)
    {
        return $"http_cache:{request.Method}:{request.RequestUri}";
    }

    private bool ShouldCache(HttpRequestMessage request, HttpResponseMessage response)
    {
        // Check cache-control headers
        if (response.Headers.CacheControl?.NoCache == true ||
            response.Headers.CacheControl?.NoStore == true)
        {
            return false;
        }

        // Only cache for a limited time
        return response.Headers.CacheControl?.MaxAge?.TotalMinutes <= 60;
    }

    private async Task CacheResponseAsync(string cacheKey, HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        var cachedResponse = new CachedResponse
        {
            StatusCode = response.StatusCode,
            Content = content,
            ContentType = response.Content.Headers.ContentType?.ToString(),
            Headers = response.Headers.ToDictionary(h => h.Key, h => h.Value.ToArray())
        };

        var expirationTime = response.Headers.CacheControl?.MaxAge ?? TimeSpan.FromMinutes(5);
        _cache.Set(cacheKey, cachedResponse, expirationTime);
    }

    private HttpResponseMessage CreateResponseFromCache(CachedResponse cachedResponse)
    {
        var response = new HttpResponseMessage(cachedResponse.StatusCode)
        {
            Content = new StringContent(cachedResponse.Content, Encoding.UTF8, cachedResponse.ContentType)
        };

        foreach (var header in cachedResponse.Headers)
        {
            response.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return response;
    }
}

public class CachedResponse
{
    public HttpStatusCode StatusCode { get; set; }
    public string Content { get; set; }
    public string ContentType { get; set; }
    public Dictionary<string, string[]> Headers { get; set; }
}
```

## Configuration Options

### HTTP Client Settings

```csharp
public class HttpClientOptions
{
    public string Type { get; set; } = "convey";
    public int Retries { get; set; } = 3;
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public Dictionary<string, ServiceOptions> Services { get; set; } = new();
}

public class ServiceOptions
{
    public string Url { get; set; }
    public int Retries { get; set; } = 3;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
    public bool EnableCircuitBreaker { get; set; } = true;
    public bool EnableRetries { get; set; } = true;
}
```

## API Reference

### IHttpClient Interface

```csharp
public interface IHttpClient
{
    Task<T> GetAsync<T>(string uri);
    Task<HttpResponseMessage> GetAsync(string uri, CancellationToken cancellationToken = default);

    Task<TResult> PostAsync<TRequest, TResult>(string uri, TRequest request, CancellationToken cancellationToken = default);
    Task<HttpResponseMessage> PostAsync<TRequest>(string uri, TRequest request, CancellationToken cancellationToken = default);

    Task<TResult> PutAsync<TRequest, TResult>(string uri, TRequest request, CancellationToken cancellationToken = default);
    Task<HttpResponseMessage> PutAsync<TRequest>(string uri, TRequest request, CancellationToken cancellationToken = default);

    Task<HttpResponseMessage> DeleteAsync(string uri, CancellationToken cancellationToken = default);
    Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default);
}
```

### Extension Methods

```csharp
public static class ConveyExtensions
{
    public static IConveyBuilder AddHttpClient(this IConveyBuilder builder, string sectionName = "httpClient");
    public static IConveyBuilder AddHttpClient(this IConveyBuilder builder, Action<HttpClientOptions> configure);
    public static IConveyBuilder AddRetryPolicies(this IConveyBuilder builder);
    public static IConveyBuilder AddCorrelationId(this IConveyBuilder builder);
}
```

## Best Practices

1. **Use typed clients** - Create strongly-typed clients for better maintainability
2. **Implement retry policies** - Handle transient failures with appropriate retry strategies
3. **Use circuit breakers** - Prevent cascading failures in distributed systems
4. **Add correlation IDs** - Enable request tracing across services
5. **Monitor HTTP calls** - Track performance and error rates
6. **Handle timeouts** - Set appropriate timeouts for different operations
7. **Use compression** - Compress large payloads to reduce bandwidth
8. **Implement caching** - Cache responses when appropriate to improve performance

## Troubleshooting

### Common Issues

1. **Service discovery failures**
   - Check service registration in discovery provider
   - Verify network connectivity between services
   - Ensure proper service naming conventions

2. **Timeout issues**
   - Adjust timeout settings based on operation complexity
   - Check network latency between services
   - Monitor service response times

3. **Retry loop issues**
   - Verify retry policies are not too aggressive
   - Check for proper exponential backoff
   - Monitor retry attempt logs

4. **Circuit breaker not opening**
   - Check failure threshold configuration
   - Verify exception handling in policies
   - Monitor service health metrics

