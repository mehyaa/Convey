# Convey.Secrets.Vault

HashiCorp Vault integration for secure secret management, providing encrypted storage, dynamic secrets, and centralized security policies for sensitive configuration data in microservices applications.

## Installation

```bash
dotnet add package Convey.Secrets.Vault
```

## Overview

Convey.Secrets.Vault provides:
- **HashiCorp Vault integration** - Secure secret storage and retrieval
- **Dynamic secrets** - Automatically rotating credentials
- **Secret engines** - Support for multiple secret backends
- **Authentication methods** - Multiple Vault authentication strategies
- **Lease management** - Automatic secret lease renewal
- **Configuration binding** - Direct binding to .NET configuration
- **Encryption/Decryption** - Transit secret engine integration
- **PKI support** - Certificate generation and management

## Configuration

### Basic Vault Setup

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddVault(vault =>
    {
        vault.Url = "https://vault.example.com:8200";
        vault.AuthType = VaultAuthType.Token;
        vault.Token = "your-vault-token";
        vault.MountPoint = "secret";
        vault.Version = VaultVersion.V2;
    });

var app = builder.Build();
app.Run();
```

### Advanced Vault Configuration

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddVault(vault =>
    {
        vault.Url = builder.Configuration["Vault:Url"];
        vault.AuthType = VaultAuthType.AppRole;
        vault.AppRole = new AppRoleOptions
        {
            RoleId = builder.Configuration["Vault:RoleId"],
            SecretId = builder.Configuration["Vault:SecretId"],
            MountPoint = "auth/approle"
        };
        vault.RetryOptions = new RetryOptions
        {
            MaxAttempts = 3,
            DelayMilliseconds = 1000,
            BackoffMultiplier = 2
        };
        vault.LeaseOptions = new LeaseOptions
        {
            RenewBuffer = TimeSpan.FromMinutes(5),
            RenewIncrement = TimeSpan.FromHours(1)
        };
        vault.SecretEngines = new[]
        {
            new SecretEngineConfig { Type = "kv", MountPoint = "secret", Version = 2 },
            new SecretEngineConfig { Type = "database", MountPoint = "database" },
            new SecretEngineConfig { Type = "transit", MountPoint = "transit" }
        };
    });

var app = builder.Build();
app.Run();
```

## Key Features

### 1. Secret Storage and Retrieval

Store and retrieve secrets from Vault:

```csharp
// Secret service interface
public interface IVaultService
{
    Task<string> GetSecretAsync(string path, string key);
    Task<T> GetSecretAsync<T>(string path) where T : class;
    Task<Dictionary<string, object>> GetSecretsAsync(string path);
    Task SetSecretAsync(string path, string key, string value);
    Task SetSecretsAsync(string path, Dictionary<string, object> secrets);
    Task DeleteSecretAsync(string path);
}

// Database configuration from Vault
public class DatabaseConfig
{
    public string ConnectionString { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public int MaxConnections { get; set; }
    public int CommandTimeout { get; set; }
}

// Service using Vault secrets
public class DatabaseService
{
    private readonly IVaultService _vaultService;
    private readonly ILogger<DatabaseService> _logger;
    private string _connectionString;

    public DatabaseService(IVaultService vaultService, ILogger<DatabaseService> logger)
    {
        _vaultService = vaultService;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        try
        {
            // Get database configuration from Vault
            var dbConfig = await _vaultService.GetSecretAsync<DatabaseConfig>("secret/database/config");

            _connectionString = $"Server={dbConfig.Server};Database={dbConfig.Database};" +
                              $"User Id={dbConfig.Username};Password={dbConfig.Password};" +
                              $"Max Pool Size={dbConfig.MaxConnections};Command Timeout={dbConfig.CommandTimeout};";

            _logger.LogInformation("Database configuration loaded from Vault");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load database configuration from Vault");
            throw;
        }
    }

    public async Task<IDbConnection> GetConnectionAsync()
    {
        if (string.IsNullOrEmpty(_connectionString))
        {
            await InitializeAsync();
        }

        return new SqlConnection(_connectionString);
    }
}

// API keys management
public class ApiKeyService
{
    private readonly IVaultService _vaultService;
    private readonly ILogger<ApiKeyService> _logger;

    public ApiKeyService(IVaultService vaultService, ILogger<ApiKeyService> logger)
    {
        _vaultService = vaultService;
        _logger = logger;
    }

    public async Task<string> GetApiKeyAsync(string serviceName)
    {
        try
        {
            var apiKey = await _vaultService.GetSecretAsync($"secret/api-keys/{serviceName}", "key");
            _logger.LogDebug("Retrieved API key for service: {ServiceName}", serviceName);
            return apiKey;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve API key for service: {ServiceName}", serviceName);
            throw;
        }
    }

    public async Task RotateApiKeyAsync(string serviceName, string newApiKey)
    {
        try
        {
            // Store new API key
            await _vaultService.SetSecretAsync($"secret/api-keys/{serviceName}", "key", newApiKey);

            // Store rotation metadata
            var metadata = new Dictionary<string, object>
            {
                ["rotated_at"] = DateTime.UtcNow.ToString("O"),
                ["rotated_by"] = Environment.UserName,
                ["version"] = await GetNextVersionAsync(serviceName)
            };

            await _vaultService.SetSecretsAsync($"secret/api-keys/{serviceName}/metadata", metadata);

            _logger.LogInformation("API key rotated for service: {ServiceName}", serviceName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rotate API key for service: {ServiceName}", serviceName);
            throw;
        }
    }

    private async Task<int> GetNextVersionAsync(string serviceName)
    {
        try
        {
            var metadata = await _vaultService.GetSecretsAsync($"secret/api-keys/{serviceName}/metadata");
            if (metadata.TryGetValue("version", out var versionObj) && int.TryParse(versionObj.ToString(), out var version))
            {
                return version + 1;
            }
        }
        catch
        {
            // Ignore errors, default to version 1
        }

        return 1;
    }
}

// Usage in controllers
[ApiController]
[Route("api/[controller]")]
public class SecretsController : ControllerBase
{
    private readonly IVaultService _vaultService;
    private readonly ApiKeyService _apiKeyService;

    public SecretsController(IVaultService vaultService, ApiKeyService apiKeyService)
    {
        _vaultService = vaultService;
        _apiKeyService = apiKeyService;
    }

    [HttpGet("health")]
    public async Task<IActionResult> GetHealth()
    {
        try
        {
            // Test Vault connectivity
            await _vaultService.GetSecretsAsync("secret/health");
            return Ok(new { Status = "Healthy", VaultConnected = true });
        }
        catch
        {
            return ServiceUnavailable(new { Status = "Unhealthy", VaultConnected = false });
        }
    }

    [HttpPost("api-keys/{serviceName}/rotate")]
    public async Task<IActionResult> RotateApiKey(string serviceName, [FromBody] RotateApiKeyRequest request)
    {
        await _apiKeyService.RotateApiKeyAsync(serviceName, request.NewApiKey);
        return Ok(new { Message = "API key rotated successfully" });
    }
}
```

### 2. Dynamic Secrets

Generate dynamic credentials from Vault:

```csharp
// Dynamic database credentials
public class DynamicDatabaseService
{
    private readonly IVaultService _vaultService;
    private readonly ILogger<DynamicDatabaseService> _logger;
    private readonly Timer _renewalTimer;
    private string _currentUsername;
    private string _currentPassword;
    private DateTime _leaseExpiry;

    public DynamicDatabaseService(IVaultService vaultService, ILogger<DynamicDatabaseService> logger)
    {
        _vaultService = vaultService;
        _logger = logger;
        _renewalTimer = new Timer(RenewCredentials, null, TimeSpan.Zero, TimeSpan.FromMinutes(30));
    }

    public async Task<string> GetConnectionStringAsync()
    {
        if (string.IsNullOrEmpty(_currentUsername) || DateTime.UtcNow.AddMinutes(5) >= _leaseExpiry)
        {
            await GenerateNewCredentialsAsync();
        }

        var server = await _vaultService.GetSecretAsync("secret/database/config", "server");
        var database = await _vaultService.GetSecretAsync("secret/database/config", "database");

        return $"Server={server};Database={database};User Id={_currentUsername};Password={_currentPassword};";
    }

    private async Task GenerateNewCredentialsAsync()
    {
        try
        {
            // Request dynamic credentials from Vault database engine
            var response = await _vaultService.GenerateDynamicCredentialsAsync("database/creds/my-role");

            _currentUsername = response.Username;
            _currentPassword = response.Password;
            _leaseExpiry = DateTime.UtcNow.AddSeconds(response.LeaseDuration);

            _logger.LogInformation("Generated new dynamic database credentials. Expires at: {ExpiryTime}", _leaseExpiry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate dynamic database credentials");
            throw;
        }
    }

    private async void RenewCredentials(object state)
    {
        try
        {
            if (DateTime.UtcNow.AddMinutes(10) >= _leaseExpiry)
            {
                await GenerateNewCredentialsAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to renew database credentials");
        }
    }

    public void Dispose()
    {
        _renewalTimer?.Dispose();
    }
}

// Dynamic AWS credentials
public class DynamicAwsService
{
    private readonly IVaultService _vaultService;
    private readonly ILogger<DynamicAwsService> _logger;
    private AwsCredentials _currentCredentials;
    private DateTime _credentialsExpiry;

    public DynamicAwsService(IVaultService vaultService, ILogger<DynamicAwsService> logger)
    {
        _vaultService = vaultService;
        _logger = logger;
    }

    public async Task<AwsCredentials> GetAwsCredentialsAsync()
    {
        if (_currentCredentials == null || DateTime.UtcNow.AddMinutes(5) >= _credentialsExpiry)
        {
            await GenerateNewAwsCredentialsAsync();
        }

        return _currentCredentials;
    }

    private async Task GenerateNewAwsCredentialsAsync()
    {
        try
        {
            // Request dynamic AWS credentials from Vault
            var response = await _vaultService.GenerateDynamicCredentialsAsync("aws/creds/s3-access-role");

            _currentCredentials = new AwsCredentials
            {
                AccessKeyId = response.AccessKey,
                SecretAccessKey = response.SecretKey,
                SessionToken = response.SecurityToken
            };

            _credentialsExpiry = DateTime.UtcNow.AddSeconds(response.LeaseDuration);

            _logger.LogInformation("Generated new dynamic AWS credentials. Expires at: {ExpiryTime}", _credentialsExpiry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate dynamic AWS credentials");
            throw;
        }
    }
}

// Dynamic certificate generation
public class CertificateService
{
    private readonly IVaultService _vaultService;
    private readonly ILogger<CertificateService> _logger;

    public CertificateService(IVaultService vaultService, ILogger<CertificateService> logger)
    {
        _vaultService = vaultService;
        _logger = logger;
    }

    public async Task<X509Certificate2> GenerateClientCertificateAsync(string commonName, TimeSpan? ttl = null)
    {
        try
        {
            var request = new
            {
                common_name = commonName,
                ttl = ttl?.TotalSeconds.ToString() ?? "8760h", // Default 1 year
                format = "pem"
            };

            var response = await _vaultService.GenerateCertificateAsync("pki/issue/client-cert", request);

            var certificate = new X509Certificate2(
                Encoding.UTF8.GetBytes(response.Certificate + response.PrivateKey),
                (string)null,
                X509KeyStorageFlags.Exportable);

            _logger.LogInformation("Generated client certificate for: {CommonName}", commonName);

            return certificate;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate client certificate for: {CommonName}", commonName);
            throw;
        }
    }

    public async Task<X509Certificate2> GenerateServerCertificateAsync(string[] dnsNames, string[] ipAddresses = null)
    {
        try
        {
            var request = new
            {
                common_name = dnsNames.FirstOrDefault(),
                alt_names = string.Join(",", dnsNames.Skip(1)),
                ip_sans = ipAddresses != null ? string.Join(",", ipAddresses) : null,
                ttl = "8760h", // 1 year
                format = "pem"
            };

            var response = await _vaultService.GenerateCertificateAsync("pki/issue/server-cert", request);

            var certificate = new X509Certificate2(
                Encoding.UTF8.GetBytes(response.Certificate + response.PrivateKey),
                (string)null,
                X509KeyStorageFlags.Exportable);

            _logger.LogInformation("Generated server certificate for: {DnsNames}", string.Join(", ", dnsNames));

            return certificate;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate server certificate for: {DnsNames}", string.Join(", ", dnsNames));
            throw;
        }
    }
}
```

### 3. Authentication Methods

Multiple Vault authentication strategies:

```csharp
// Token authentication
public class TokenAuthProvider : IVaultAuthProvider
{
    private readonly string _token;

    public TokenAuthProvider(string token)
    {
        _token = token;
    }

    public async Task<string> GetTokenAsync()
    {
        return _token;
    }
}

// AppRole authentication
public class AppRoleAuthProvider : IVaultAuthProvider
{
    private readonly string _roleId;
    private readonly string _secretId;
    private readonly string _mountPoint;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AppRoleAuthProvider> _logger;
    private string _cachedToken;
    private DateTime _tokenExpiry;

    public AppRoleAuthProvider(
        string roleId,
        string secretId,
        string mountPoint,
        IHttpClientFactory httpClientFactory,
        ILogger<AppRoleAuthProvider> logger)
    {
        _roleId = roleId;
        _secretId = secretId;
        _mountPoint = mountPoint;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<string> GetTokenAsync()
    {
        if (!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow < _tokenExpiry.AddMinutes(-5))
        {
            return _cachedToken;
        }

        return await AuthenticateAsync();
    }

    private async Task<string> AuthenticateAsync()
    {
        try
        {
            using var client = _httpClientFactory.CreateClient();

            var loginRequest = new
            {
                role_id = _roleId,
                secret_id = _secretId
            };

            var response = await client.PostAsJsonAsync($"{_mountPoint}/login", loginRequest);
            response.EnsureSuccessStatusCode();

            var loginResponse = await response.Content.ReadFromJsonAsync<VaultAuthResponse>();

            _cachedToken = loginResponse.Auth.ClientToken;
            _tokenExpiry = DateTime.UtcNow.AddSeconds(loginResponse.Auth.LeaseDuration);

            _logger.LogInformation("Successfully authenticated with Vault using AppRole");

            return _cachedToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to authenticate with Vault using AppRole");
            throw;
        }
    }
}

// Kubernetes authentication
public class KubernetesAuthProvider : IVaultAuthProvider
{
    private readonly string _role;
    private readonly string _mountPoint;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<KubernetesAuthProvider> _logger;

    public KubernetesAuthProvider(
        string role,
        string mountPoint,
        IHttpClientFactory httpClientFactory,
        ILogger<KubernetesAuthProvider> logger)
    {
        _role = role;
        _mountPoint = mountPoint;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<string> GetTokenAsync()
    {
        try
        {
            // Read Kubernetes service account token
            var jwt = await File.ReadAllTextAsync("/var/run/secrets/kubernetes.io/serviceaccount/token");

            using var client = _httpClientFactory.CreateClient();

            var loginRequest = new
            {
                role = _role,
                jwt = jwt
            };

            var response = await client.PostAsJsonAsync($"{_mountPoint}/login", loginRequest);
            response.EnsureSuccessStatusCode();

            var loginResponse = await response.Content.ReadFromJsonAsync<VaultAuthResponse>();

            _logger.LogInformation("Successfully authenticated with Vault using Kubernetes auth");

            return loginResponse.Auth.ClientToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to authenticate with Vault using Kubernetes auth");
            throw;
        }
    }
}

// AWS IAM authentication
public class AwsIamAuthProvider : IVaultAuthProvider
{
    private readonly string _role;
    private readonly string _mountPoint;
    private readonly IAmazonSecurityTokenService _stsClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AwsIamAuthProvider> _logger;

    public AwsIamAuthProvider(
        string role,
        string mountPoint,
        IAmazonSecurityTokenService stsClient,
        IHttpClientFactory httpClientFactory,
        ILogger<AwsIamAuthProvider> logger)
    {
        _role = role;
        _mountPoint = mountPoint;
        _stsClient = stsClient;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<string> GetTokenAsync()
    {
        try
        {
            // Generate STS request
            var request = new GetCallerIdentityRequest();
            var callerIdentity = await _stsClient.GetCallerIdentityAsync(request);

            // Create signed request for Vault
            var iamRequest = CreateIamRequest();
            var signature = SignRequest(iamRequest);

            using var client = _httpClientFactory.CreateClient();

            var loginRequest = new
            {
                role = _role,
                iam_http_request_method = "POST",
                iam_request_url = Convert.ToBase64String(Encoding.UTF8.GetBytes("https://sts.amazonaws.com/")),
                iam_request_body = Convert.ToBase64String(Encoding.UTF8.GetBytes(iamRequest)),
                iam_request_headers = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(signature)))
            };

            var response = await client.PostAsJsonAsync($"{_mountPoint}/login", loginRequest);
            response.EnsureSuccessStatusCode();

            var loginResponse = await response.Content.ReadFromJsonAsync<VaultAuthResponse>();

            _logger.LogInformation("Successfully authenticated with Vault using AWS IAM");

            return loginResponse.Auth.ClientToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to authenticate with Vault using AWS IAM");
            throw;
        }
    }

    private string CreateIamRequest()
    {
        return "Action=GetCallerIdentity&Version=2011-06-15";
    }

    private Dictionary<string, string> SignRequest(string requestBody)
    {
        // AWS signature v4 implementation
        // This is a simplified version - use AWS SDK for production
        return new Dictionary<string, string>
        {
            ["Authorization"] = "AWS4-HMAC-SHA256 ...",
            ["X-Amz-Date"] = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ"),
            ["Content-Type"] = "application/x-www-form-urlencoded; charset=utf-8"
        };
    }
}

// Authentication factory
public class VaultAuthProviderFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;

    public VaultAuthProviderFactory(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
    }

    public IVaultAuthProvider CreateAuthProvider(VaultAuthType authType)
    {
        return authType switch
        {
            VaultAuthType.Token => new TokenAuthProvider(_configuration["Vault:Token"]),
            VaultAuthType.AppRole => _serviceProvider.GetRequiredService<AppRoleAuthProvider>(),
            VaultAuthType.Kubernetes => _serviceProvider.GetRequiredService<KubernetesAuthProvider>(),
            VaultAuthType.AwsIam => _serviceProvider.GetRequiredService<AwsIamAuthProvider>(),
            _ => throw new ArgumentException($"Unsupported auth type: {authType}")
        };
    }
}
```

### 4. Transit Engine Integration

Encryption and decryption using Vault transit engine:

```csharp
// Transit encryption service
public interface IVaultTransitService
{
    Task<string> EncryptAsync(string keyName, string plaintext);
    Task<string> DecryptAsync(string keyName, string ciphertext);
    Task<string> EncryptAsync(string keyName, byte[] plaintext);
    Task<byte[]> DecryptBytesAsync(string keyName, string ciphertext);
    Task<Dictionary<string, string>> EncryptBatchAsync(string keyName, Dictionary<string, string> plaintexts);
    Task<Dictionary<string, string>> DecryptBatchAsync(string keyName, Dictionary<string, string> ciphertexts);
}

public class VaultTransitService : IVaultTransitService
{
    private readonly IVaultService _vaultService;
    private readonly ILogger<VaultTransitService> _logger;

    public VaultTransitService(IVaultService vaultService, ILogger<VaultTransitService> logger)
    {
        _vaultService = vaultService;
        _logger = logger;
    }

    public async Task<string> EncryptAsync(string keyName, string plaintext)
    {
        try
        {
            var request = new
            {
                plaintext = Convert.ToBase64String(Encoding.UTF8.GetBytes(plaintext))
            };

            var response = await _vaultService.PostAsync($"transit/encrypt/{keyName}", request);
            var ciphertext = response["ciphertext"].ToString();

            _logger.LogDebug("Successfully encrypted data using key: {KeyName}", keyName);
            return ciphertext;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to encrypt data using key: {KeyName}", keyName);
            throw;
        }
    }

    public async Task<string> DecryptAsync(string keyName, string ciphertext)
    {
        try
        {
            var request = new
            {
                ciphertext = ciphertext
            };

            var response = await _vaultService.PostAsync($"transit/decrypt/{keyName}", request);
            var plaintextBase64 = response["plaintext"].ToString();
            var plaintext = Encoding.UTF8.GetString(Convert.FromBase64String(plaintextBase64));

            _logger.LogDebug("Successfully decrypted data using key: {KeyName}", keyName);
            return plaintext;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt data using key: {KeyName}", keyName);
            throw;
        }
    }

    public async Task<Dictionary<string, string>> EncryptBatchAsync(string keyName, Dictionary<string, string> plaintexts)
    {
        try
        {
            var batchInput = plaintexts.Select(kvp => new
            {
                plaintext = Convert.ToBase64String(Encoding.UTF8.GetBytes(kvp.Value)),
                context = Convert.ToBase64String(Encoding.UTF8.GetBytes(kvp.Key))
            }).ToArray();

            var request = new
            {
                batch_input = batchInput
            };

            var response = await _vaultService.PostAsync($"transit/encrypt/{keyName}", request);
            var batchResults = JsonSerializer.Deserialize<Dictionary<string, object>[]>(response["batch_results"].ToString());

            var results = new Dictionary<string, string>();
            for (int i = 0; i < plaintexts.Count; i++)
            {
                var key = plaintexts.Keys.ElementAt(i);
                var ciphertext = batchResults[i]["ciphertext"].ToString();
                results[key] = ciphertext;
            }

            _logger.LogDebug("Successfully encrypted batch of {Count} items using key: {KeyName}", plaintexts.Count, keyName);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to encrypt batch using key: {KeyName}", keyName);
            throw;
        }
    }
}

// Data protection using Transit engine
public class VaultDataProtectionService
{
    private readonly IVaultTransitService _transitService;
    private readonly ILogger<VaultDataProtectionService> _logger;
    private const string DefaultKeyName = "application-data";

    public VaultDataProtectionService(IVaultTransitService transitService, ILogger<VaultDataProtectionService> logger)
    {
        _transitService = transitService;
        _logger = logger;
    }

    public async Task<string> ProtectAsync(string data, string purpose = null)
    {
        var keyName = purpose ?? DefaultKeyName;
        return await _transitService.EncryptAsync(keyName, data);
    }

    public async Task<string> UnprotectAsync(string protectedData, string purpose = null)
    {
        var keyName = purpose ?? DefaultKeyName;
        return await _transitService.DecryptAsync(keyName, protectedData);
    }

    public async Task<T> ProtectObjectAsync<T>(T obj, string purpose = null) where T : class
    {
        var json = JsonSerializer.Serialize(obj);
        var protectedJson = await ProtectAsync(json, purpose);

        return JsonSerializer.Deserialize<T>($"{{\"_protected\":\"{protectedJson}\"}}");
    }

    public async Task<T> UnprotectObjectAsync<T>(T protectedObj, string purpose = null) where T : class
    {
        var protectedJson = JsonSerializer.Serialize(protectedObj);
        var protectedData = JsonSerializer.Deserialize<Dictionary<string, string>>(protectedJson)["_protected"];
        var json = await UnprotectAsync(protectedData, purpose);

        return JsonSerializer.Deserialize<T>(json);
    }
}

// Usage in models
public class EncryptedUser
{
    public Guid Id { get; set; }
    public string Username { get; set; }

    [VaultEncrypted("user-pii")]
    public string Email { get; set; }

    [VaultEncrypted("user-pii")]
    public string PhoneNumber { get; set; }

    [VaultEncrypted("user-sensitive")]
    public string SocialSecurityNumber { get; set; }

    public DateTime CreatedAt { get; set; }
}

// Attribute for marking encrypted properties
[AttributeUsage(AttributeTargets.Property)]
public class VaultEncryptedAttribute : Attribute
{
    public string KeyName { get; }

    public VaultEncryptedAttribute(string keyName = null)
    {
        KeyName = keyName ?? "default";
    }
}

// Entity Framework converter for encrypted properties
public class VaultEncryptedConverter : ValueConverter<string, string>
{
    public VaultEncryptedConverter(IVaultTransitService transitService, string keyName)
        : base(
            v => transitService.EncryptAsync(keyName, v).GetAwaiter().GetResult(),
            v => transitService.DecryptAsync(keyName, v).GetAwaiter().GetResult())
    {
    }
}
```

## Configuration Options

### Vault Options

```csharp
public class VaultOptions
{
    public string Url { get; set; }
    public VaultAuthType AuthType { get; set; }
    public string Token { get; set; }
    public AppRoleOptions AppRole { get; set; }
    public string MountPoint { get; set; } = "secret";
    public VaultVersion Version { get; set; } = VaultVersion.V2;
    public RetryOptions RetryOptions { get; set; } = new();
    public LeaseOptions LeaseOptions { get; set; } = new();
    public SecretEngineConfig[] SecretEngines { get; set; } = Array.Empty<SecretEngineConfig>();
}

public class AppRoleOptions
{
    public string RoleId { get; set; }
    public string SecretId { get; set; }
    public string MountPoint { get; set; } = "auth/approle";
}

public enum VaultAuthType
{
    Token,
    AppRole,
    Kubernetes,
    AwsIam
}
```

## API Reference

### Extension Methods

```csharp
public static class ConveyExtensions
{
    public static IConveyBuilder AddVault(this IConveyBuilder builder, Action<VaultOptions> configure = null);
    public static IConveyBuilder AddVaultTransit(this IConveyBuilder builder);
}
```

## Best Practices

1. **Use AppRole for applications** - More secure than static tokens
2. **Implement lease renewal** - Automatically renew expiring secrets
3. **Rotate secrets regularly** - Use dynamic secrets when possible
4. **Encrypt sensitive data** - Use transit engine for application-level encryption
5. **Monitor Vault access** - Enable audit logging and monitoring
6. **Use appropriate TTLs** - Set reasonable time-to-live values
7. **Secure Vault communication** - Always use TLS/mTLS
8. **Handle failures gracefully** - Implement proper error handling and fallbacks

## Troubleshooting

### Common Issues

1. **Authentication failures**
   - Check Vault URL and authentication credentials
   - Verify network connectivity to Vault
   - Ensure proper Vault policies are configured

2. **Secret retrieval errors**
   - Verify secret path and mount point
   - Check Vault policies and permissions
   - Ensure secret exists and is accessible

3. **Lease renewal failures**
   - Check token permissions for lease renewal
   - Verify lease renewal buffer settings
   - Monitor Vault audit logs for errors

4. **Transit encryption errors**
   - Ensure transit engine is enabled
   - Verify encryption key exists
   - Check key permissions and policies
