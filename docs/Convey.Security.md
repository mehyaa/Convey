---
layout: default
title: Convey.Security
parent: Authentication & Security
---
# Convey.Security

Core security abstractions and utilities providing encryption, hashing, authentication helpers, and security policy enforcement for Convey-based applications with standardized security implementations.

## Installation

```bash
dotnet add package Convey.Security
```

## Overview

Convey.Security provides:
- **Encryption services** - AES encryption with key management
- **Hashing utilities** - Password hashing and data integrity
- **Token generation** - Secure token and API key generation
- **Authentication helpers** - User authentication abstractions
- **Authorization utilities** - Permission and role management
- **Data protection** - Sensitive data protection mechanisms
- **Security validation** - Input validation and sanitization
- **Audit logging** - Security event tracking and monitoring

## Configuration

### Basic Security Setup

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddSecurity(security =>
    {
        security.Encryption.Key = builder.Configuration["Security:EncryptionKey"];
        security.Hashing.Algorithm = HashingAlgorithm.BCrypt;
        security.Hashing.WorkFactor = 12;
        security.Tokens.Secret = builder.Configuration["Security:TokenSecret"];
        security.Tokens.ExpiryMinutes = 60;
    });

var app = builder.Build();
app.Run();
```

### Advanced Security Configuration

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddSecurity(security =>
    {
        security.Encryption = new EncryptionOptions
        {
            Algorithm = EncryptionAlgorithm.AES256,
            Key = builder.Configuration["Security:EncryptionKey"],
            KeyDerivation = KeyDerivationFunction.PBKDF2,
            Salt = builder.Configuration["Security:Salt"],
            Iterations = 100000
        };

        security.Hashing = new HashingOptions
        {
            Algorithm = HashingAlgorithm.Argon2,
            WorkFactor = 16,
            SaltSize = 32,
            HashSize = 64
        };

        security.Tokens = new TokenOptions
        {
            Secret = builder.Configuration["Security:TokenSecret"],
            Issuer = "Convey.Security",
            Audience = "api-consumers",
            ExpiryMinutes = 15,
            RefreshTokenExpiryDays = 30,
            Algorithm = TokenAlgorithm.HS256
        };

        security.DataProtection = new DataProtectionOptions
        {
            KeyLifetime = TimeSpan.FromDays(90),
            ApplicationName = "ConveyApp",
            PersistKeysToFileSystem = true,
            KeyDirectory = "/app/keys"
        };

        security.Audit = new AuditOptions
        {
            EnabledEvents = SecurityEvent.All,
            RetentionDays = 365,
            IncludeDetails = true
        };
    });

var app = builder.Build();
app.Run();
```

## Key Features

### 1. Encryption Services

AES encryption with secure key management:

```csharp
// Encryption service interface
public interface IEncryptionService
{
    string Encrypt(string plaintext);
    string Decrypt(string ciphertext);
    byte[] Encrypt(byte[] plaintext);
    byte[] Decrypt(byte[] ciphertext);
    string EncryptWithTimestamp(string plaintext);
    string DecryptWithTimestamp(string ciphertext, TimeSpan? maxAge = null);
}

// Implementation
public class AesEncryptionService : IEncryptionService
{
    private readonly byte[] _key;
    private readonly ILogger<AesEncryptionService> _logger;

    public AesEncryptionService(EncryptionOptions options, ILogger<AesEncryptionService> logger)
    {
        _key = DeriveKey(options.Key, options.Salt, options.Iterations);
        _logger = logger;
    }

    public string Encrypt(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
            return plaintext;

        try
        {
            using var aes = Aes.Create();
            aes.Key = _key;
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor();
            using var ms = new MemoryStream();
            using var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write);
            using var writer = new StreamWriter(cs);

            writer.Write(plaintext);
            writer.Flush();
            cs.FlushFinalBlock();

            var encrypted = ms.ToArray();
            var result = new byte[aes.IV.Length + encrypted.Length];

            Array.Copy(aes.IV, 0, result, 0, aes.IV.Length);
            Array.Copy(encrypted, 0, result, aes.IV.Length, encrypted.Length);

            return Convert.ToBase64String(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to encrypt data");
            throw new SecurityException("Encryption failed", ex);
        }
    }

    public string Decrypt(string ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext))
            return ciphertext;

        try
        {
            var data = Convert.FromBase64String(ciphertext);

            using var aes = Aes.Create();
            aes.Key = _key;

            var iv = new byte[aes.IV.Length];
            var encrypted = new byte[data.Length - iv.Length];

            Array.Copy(data, 0, iv, 0, iv.Length);
            Array.Copy(data, iv.Length, encrypted, 0, encrypted.Length);

            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            using var ms = new MemoryStream(encrypted);
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var reader = new StreamReader(cs);

            return reader.ReadToEnd();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt data");
            throw new SecurityException("Decryption failed", ex);
        }
    }

    public string EncryptWithTimestamp(string plaintext)
    {
        var timestampedData = new TimestampedData
        {
            Data = plaintext,
            Timestamp = DateTimeOffset.UtcNow
        };

        var json = JsonSerializer.Serialize(timestampedData);
        return Encrypt(json);
    }

    public string DecryptWithTimestamp(string ciphertext, TimeSpan? maxAge = null)
    {
        var json = Decrypt(ciphertext);
        var timestampedData = JsonSerializer.Deserialize<TimestampedData>(json);

        if (maxAge.HasValue)
        {
            var age = DateTimeOffset.UtcNow - timestampedData.Timestamp;
            if (age > maxAge.Value)
            {
                throw new SecurityException("Encrypted data has expired");
            }
        }

        return timestampedData.Data;
    }

    private byte[] DeriveKey(string password, string salt, int iterations)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(password, Encoding.UTF8.GetBytes(salt), iterations, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(32); // 256-bit key
    }
}

// Timestamped data model
public class TimestampedData
{
    public string Data { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}

// Usage examples
public class UserService
{
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<UserService> _logger;

    public UserService(IEncryptionService encryptionService, ILogger<UserService> logger)
    {
        _encryptionService = encryptionService;
        _logger = logger;
    }

    public async Task<User> CreateUserAsync(CreateUserRequest request)
    {
        // Encrypt sensitive data
        var encryptedSsn = _encryptionService.Encrypt(request.SocialSecurityNumber);
        var encryptedPhone = _encryptionService.Encrypt(request.PhoneNumber);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            Name = request.Name,
            EncryptedSocialSecurityNumber = encryptedSsn,
            EncryptedPhoneNumber = encryptedPhone,
            CreatedAt = DateTime.UtcNow
        };

        _logger.LogInformation("Created user with encrypted PII: {UserId}", user.Id);
        return user;
    }

    public UserDetailsDto GetUserDetails(User user)
    {
        // Decrypt sensitive data for display
        var ssn = _encryptionService.Decrypt(user.EncryptedSocialSecurityNumber);
        var phone = _encryptionService.Decrypt(user.EncryptedPhoneNumber);

        return new UserDetailsDto
        {
            Id = user.Id,
            Email = user.Email,
            Name = user.Name,
            SocialSecurityNumber = MaskSsn(ssn),
            PhoneNumber = phone,
            CreatedAt = user.CreatedAt
        };
    }

    private string MaskSsn(string ssn)
    {
        if (ssn.Length >= 4)
            return "***-**-" + ssn.Substring(ssn.Length - 4);
        return "***-**-****";
    }
}
```

### 2. Password Hashing

Secure password hashing with multiple algorithms:

```csharp
// Password hasher interface
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hashedPassword);
    bool NeedsRehash(string hashedPassword);
}

// BCrypt implementation
public class BCryptPasswordHasher : IPasswordHasher
{
    private readonly int _workFactor;
    private readonly ILogger<BCryptPasswordHasher> _logger;

    public BCryptPasswordHasher(HashingOptions options, ILogger<BCryptPasswordHasher> logger)
    {
        _workFactor = options.WorkFactor;
        _logger = logger;
    }

    public string Hash(string password)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password cannot be null or empty", nameof(password));

        try
        {
            return BCrypt.Net.BCrypt.HashPassword(password, _workFactor);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to hash password");
            throw new SecurityException("Password hashing failed", ex);
        }
    }

    public bool Verify(string password, string hashedPassword)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hashedPassword))
            return false;

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify password");
            return false;
        }
    }

    public bool NeedsRehash(string hashedPassword)
    {
        if (string.IsNullOrEmpty(hashedPassword))
            return true;

        try
        {
            // Check if the hash was created with a different work factor
            var hashInfo = BCrypt.Net.BCrypt.InterrogateHash(hashedPassword);
            return hashInfo.WorkFactor != _workFactor;
        }
        catch
        {
            return true; // If we can't parse the hash, it needs rehashing
        }
    }
}

// Argon2 implementation
public class Argon2PasswordHasher : IPasswordHasher
{
    private readonly int _workFactor;
    private readonly int _saltSize;
    private readonly int _hashSize;
    private readonly ILogger<Argon2PasswordHasher> _logger;

    public Argon2PasswordHasher(HashingOptions options, ILogger<Argon2PasswordHasher> logger)
    {
        _workFactor = options.WorkFactor;
        _saltSize = options.SaltSize;
        _hashSize = options.HashSize;
        _logger = logger;
    }

    public string Hash(string password)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password cannot be null or empty", nameof(password));

        try
        {
            using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password));

            argon2.Salt = GenerateSalt();
            argon2.DegreeOfParallelism = Environment.ProcessorCount;
            argon2.Iterations = _workFactor;
            argon2.MemorySize = 65536; // 64 MB

            var hash = argon2.GetBytes(_hashSize);

            // Combine salt and hash for storage
            var combined = new byte[argon2.Salt.Length + hash.Length];
            Array.Copy(argon2.Salt, 0, combined, 0, argon2.Salt.Length);
            Array.Copy(hash, 0, combined, argon2.Salt.Length, hash.Length);

            return Convert.ToBase64String(combined);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to hash password with Argon2");
            throw new SecurityException("Password hashing failed", ex);
        }
    }

    public bool Verify(string password, string hashedPassword)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hashedPassword))
            return false;

        try
        {
            var combined = Convert.FromBase64String(hashedPassword);
            var salt = new byte[_saltSize];
            var hash = new byte[_hashSize];

            Array.Copy(combined, 0, salt, 0, _saltSize);
            Array.Copy(combined, _saltSize, hash, 0, _hashSize);

            using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password));
            argon2.Salt = salt;
            argon2.DegreeOfParallelism = Environment.ProcessorCount;
            argon2.Iterations = _workFactor;
            argon2.MemorySize = 65536;

            var computedHash = argon2.GetBytes(_hashSize);
            return hash.SequenceEqual(computedHash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify password with Argon2");
            return false;
        }
    }

    public bool NeedsRehash(string hashedPassword)
    {
        // For Argon2, we'd need to store parameters separately or parse them
        // This is a simplified version
        return false;
    }

    private byte[] GenerateSalt()
    {
        var salt = new byte[_saltSize];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(salt);
        return salt;
    }
}

// Authentication service using password hasher
public class AuthenticationService
{
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUserRepository _userRepository;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AuthenticationService> _logger;

    public AuthenticationService(
        IPasswordHasher passwordHasher,
        IUserRepository userRepository,
        ITokenService tokenService,
        ILogger<AuthenticationService> logger)
    {
        _passwordHasher = passwordHasher;
        _userRepository = userRepository;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<AuthenticationResult> AuthenticateAsync(string email, string password)
    {
        try
        {
            var user = await _userRepository.GetByEmailAsync(email);
            if (user == null)
            {
                _logger.LogWarning("Authentication failed: user not found for email {Email}", email);
                return AuthenticationResult.Failed("Invalid credentials");
            }

            if (!_passwordHasher.Verify(password, user.PasswordHash))
            {
                _logger.LogWarning("Authentication failed: invalid password for user {UserId}", user.Id);
                return AuthenticationResult.Failed("Invalid credentials");
            }

            // Check if password needs rehashing
            if (_passwordHasher.NeedsRehash(user.PasswordHash))
            {
                user.PasswordHash = _passwordHasher.Hash(password);
                await _userRepository.UpdateAsync(user);
                _logger.LogInformation("Password rehashed for user {UserId}", user.Id);
            }

            var token = await _tokenService.GenerateTokenAsync(user);

            _logger.LogInformation("User authenticated successfully: {UserId}", user.Id);

            return AuthenticationResult.Success(user, token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication error for email {Email}", email);
            return AuthenticationResult.Failed("Authentication failed");
        }
    }

    public async Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                return false;

            if (!_passwordHasher.Verify(currentPassword, user.PasswordHash))
            {
                _logger.LogWarning("Password change failed: invalid current password for user {UserId}", userId);
                return false;
            }

            user.PasswordHash = _passwordHasher.Hash(newPassword);
            user.PasswordChangedAt = DateTime.UtcNow;

            await _userRepository.UpdateAsync(user);

            _logger.LogInformation("Password changed for user {UserId}", userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to change password for user {UserId}", userId);
            return false;
        }
    }
}
```

### 3. Token Generation and Management

Secure token generation for API keys and access tokens:

```csharp
// Token service interface
public interface ITokenService
{
    Task<string> GenerateTokenAsync(User user, TimeSpan? expiry = null);
    Task<string> GenerateRefreshTokenAsync(User user);
    Task<string> GenerateApiKeyAsync(string purpose, TimeSpan? expiry = null);
    Task<bool> ValidateTokenAsync(string token);
    Task<ClaimsPrincipal> GetPrincipalFromTokenAsync(string token);
    Task RevokeTokenAsync(string token);
}

// JWT token service implementation
public class JwtTokenService : ITokenService
{
    private readonly TokenOptions _options;
    private readonly ITokenRepository _tokenRepository;
    private readonly ILogger<JwtTokenService> _logger;

    public JwtTokenService(
        TokenOptions options,
        ITokenRepository tokenRepository,
        ILogger<JwtTokenService> logger)
    {
        _options = options;
        _tokenRepository = tokenRepository;
        _logger = logger;
    }

    public async Task<string> GenerateTokenAsync(User user, TimeSpan? expiry = null)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_options.Secret);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.Name),
                new(ClaimTypes.Email, user.Email),
                new("jti", Guid.NewGuid().ToString()),
                new("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
            };

            // Add role claims
            foreach (var role in user.Roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            // Add permission claims
            foreach (var permission in user.Permissions)
            {
                claims.Add(new Claim("permission", permission));
            }

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.Add(expiry ?? TimeSpan.FromMinutes(_options.ExpiryMinutes)),
                Issuer = _options.Issuer,
                Audience = _options.Audience,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(token);

            // Store token metadata for tracking
            await _tokenRepository.StoreTokenAsync(new TokenMetadata
            {
                JwtId = claims.First(c => c.Type == "jti").Value,
                UserId = user.Id,
                TokenType = TokenType.Access,
                IssuedAt = DateTime.UtcNow,
                ExpiresAt = tokenDescriptor.Expires.Value,
                IsRevoked = false
            });

            _logger.LogInformation("Generated access token for user {UserId}", user.Id);
            return tokenString;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate token for user {UserId}", user.Id);
            throw new SecurityException("Token generation failed", ex);
        }
    }

    public async Task<string> GenerateRefreshTokenAsync(User user)
    {
        try
        {
            var refreshToken = GenerateSecureRandomString(64);

            await _tokenRepository.StoreTokenAsync(new TokenMetadata
            {
                JwtId = refreshToken,
                UserId = user.Id,
                TokenType = TokenType.Refresh,
                IssuedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(_options.RefreshTokenExpiryDays),
                IsRevoked = false
            });

            _logger.LogInformation("Generated refresh token for user {UserId}", user.Id);
            return refreshToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate refresh token for user {UserId}", user.Id);
            throw new SecurityException("Refresh token generation failed", ex);
        }
    }

    public async Task<string> GenerateApiKeyAsync(string purpose, TimeSpan? expiry = null)
    {
        try
        {
            var apiKey = $"cvk_{GenerateSecureRandomString(32)}";

            await _tokenRepository.StoreTokenAsync(new TokenMetadata
            {
                JwtId = apiKey,
                TokenType = TokenType.ApiKey,
                Purpose = purpose,
                IssuedAt = DateTime.UtcNow,
                ExpiresAt = expiry.HasValue ? DateTime.UtcNow.Add(expiry.Value) : DateTime.MaxValue,
                IsRevoked = false
            });

            _logger.LogInformation("Generated API key for purpose: {Purpose}", purpose);
            return apiKey;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate API key for purpose: {Purpose}", purpose);
            throw new SecurityException("API key generation failed", ex);
        }
    }

    public async Task<bool> ValidateTokenAsync(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_options.Secret);

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _options.Issuer,
                ValidAudience = _options.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ClockSkew = TimeSpan.Zero
            };

            var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);
            var jwtToken = validatedToken as JwtSecurityToken;

            if (jwtToken == null)
                return false;

            // Check if token is revoked
            var jti = jwtToken.Claims.FirstOrDefault(x => x.Type == "jti")?.Value;
            if (!string.IsNullOrEmpty(jti))
            {
                var tokenMetadata = await _tokenRepository.GetTokenAsync(jti);
                if (tokenMetadata != null && tokenMetadata.IsRevoked)
                {
                    _logger.LogWarning("Attempted to use revoked token: {Jti}", jti);
                    return false;
                }
            }

            return true;
        }
        catch (SecurityTokenExpiredException)
        {
            _logger.LogDebug("Token validation failed: token expired");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Token validation failed");
            return false;
        }
    }

    public async Task<ClaimsPrincipal> GetPrincipalFromTokenAsync(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_options.Secret);

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = false, // We want to get principal even from expired tokens
                ValidateIssuerSigningKey = true,
                ValidIssuer = _options.Issuer,
                ValidAudience = _options.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ClockSkew = TimeSpan.Zero
            };

            var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);
            return principal;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get principal from token");
            return null;
        }
    }

    public async Task RevokeTokenAsync(string token)
    {
        try
        {
            var principal = await GetPrincipalFromTokenAsync(token);
            if (principal == null)
                return;

            var jti = principal.FindFirst("jti")?.Value;
            if (string.IsNullOrEmpty(jti))
                return;

            await _tokenRepository.RevokeTokenAsync(jti);
            _logger.LogInformation("Revoked token: {Jti}", jti);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to revoke token");
            throw new SecurityException("Token revocation failed", ex);
        }
    }

    private string GenerateSecureRandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[length];
        rng.GetBytes(bytes);

        return new string(bytes.Select(b => chars[b % chars.Length]).ToArray());
    }
}

// Token metadata model
public class TokenMetadata
{
    public string JwtId { get; set; }
    public Guid? UserId { get; set; }
    public TokenType TokenType { get; set; }
    public string Purpose { get; set; }
    public DateTime IssuedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime? RevokedAt { get; set; }
}

public enum TokenType
{
    Access,
    Refresh,
    ApiKey
}
```

### 4. Data Protection and Validation

Input validation and data protection utilities:

```csharp
// Security validator interface
public interface ISecurityValidator
{
    bool IsValidEmail(string email);
    bool IsStrongPassword(string password);
    bool IsSafeInput(string input);
    bool IsValidUrl(string url);
    bool IsValidPhoneNumber(string phoneNumber);
    ValidationResult ValidateUser(CreateUserRequest request);
}

// Security validator implementation
public class SecurityValidator : ISecurityValidator
{
    private readonly ILogger<SecurityValidator> _logger;
    private readonly SecurityValidationOptions _options;

    public SecurityValidator(SecurityValidationOptions options, ILogger<SecurityValidator> logger)
    {
        _options = options;
        _logger = logger;
    }

    public bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        try
        {
            var addr = new MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    public bool IsStrongPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
            return false;

        // Check minimum length
        if (password.Length < _options.MinPasswordLength)
            return false;

        // Check for required character types
        var hasUpper = password.Any(char.IsUpper);
        var hasLower = password.Any(char.IsLower);
        var hasDigit = password.Any(char.IsDigit);
        var hasSpecial = password.Any(c => !char.IsLetterOrDigit(c));

        var requiredTypes = 0;
        if (_options.RequireUppercase && hasUpper) requiredTypes++;
        if (_options.RequireLowercase && hasLower) requiredTypes++;
        if (_options.RequireDigit && hasDigit) requiredTypes++;
        if (_options.RequireSpecialCharacter && hasSpecial) requiredTypes++;

        return requiredTypes >= _options.MinRequiredCharacterTypes;
    }

    public bool IsSafeInput(string input)
    {
        if (string.IsNullOrEmpty(input))
            return true;

        // Check for common XSS patterns
        var xssPatterns = new[]
        {
            @"<script[^>]*>.*?</script>",
            @"javascript:",
            @"vbscript:",
            @"onload\s*=",
            @"onerror\s*=",
            @"onclick\s*=",
            @"eval\s*\(",
            @"expression\s*\("
        };

        foreach (var pattern in xssPatterns)
        {
            if (Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase))
            {
                _logger.LogWarning("Potentially unsafe input detected: {Pattern}", pattern);
                return false;
            }
        }

        // Check for SQL injection patterns
        var sqlPatterns = new[]
        {
            @"(\b(ALTER|CREATE|DELETE|DROP|EXEC(UTE){0,1}|INSERT( +INTO){0,1}|MERGE|SELECT|UNION|UPDATE)\b)",
            @"(\b(AND|OR)\b.{1,6}?(=|>|<|\bin\b|\blike\b))",
            @"(\bEXEC\b.{0,10}?\()",
            @"(\b(sp_|xp_|sp_executesql)\b)"
        };

        foreach (var pattern in sqlPatterns)
        {
            if (Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase))
            {
                _logger.LogWarning("Potentially unsafe SQL input detected: {Pattern}", pattern);
                return false;
            }
        }

        return true;
    }

    public bool IsValidUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        return Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    public bool IsValidPhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return false;

        // Simple international phone number validation
        var pattern = @"^\+?[1-9]\d{1,14}$";
        return Regex.IsMatch(phoneNumber.Replace(" ", "").Replace("-", ""), pattern);
    }

    public ValidationResult ValidateUser(CreateUserRequest request)
    {
        var result = new ValidationResult();

        if (!IsValidEmail(request.Email))
        {
            result.AddError("Email", "Invalid email format");
        }

        if (!IsStrongPassword(request.Password))
        {
            result.AddError("Password", "Password does not meet security requirements");
        }

        if (!IsSafeInput(request.Name))
        {
            result.AddError("Name", "Name contains potentially unsafe characters");
        }

        if (!string.IsNullOrEmpty(request.PhoneNumber) && !IsValidPhoneNumber(request.PhoneNumber))
        {
            result.AddError("PhoneNumber", "Invalid phone number format");
        }

        return result;
    }
}

// Validation result
public class ValidationResult
{
    public bool IsValid => !Errors.Any();
    public Dictionary<string, List<string>> Errors { get; } = new();

    public void AddError(string field, string message)
    {
        if (!Errors.ContainsKey(field))
        {
            Errors[field] = new List<string>();
        }
        Errors[field].Add(message);
    }
}

// Security validation options
public class SecurityValidationOptions
{
    public int MinPasswordLength { get; set; } = 8;
    public bool RequireUppercase { get; set; } = true;
    public bool RequireLowercase { get; set; } = true;
    public bool RequireDigit { get; set; } = true;
    public bool RequireSpecialCharacter { get; set; } = true;
    public int MinRequiredCharacterTypes { get; set; } = 3;
}
```

## Configuration Options

### Security Options

```csharp
public class SecurityOptions
{
    public EncryptionOptions Encryption { get; set; } = new();
    public HashingOptions Hashing { get; set; } = new();
    public TokenOptions Tokens { get; set; } = new();
    public DataProtectionOptions DataProtection { get; set; } = new();
    public AuditOptions Audit { get; set; } = new();
}

public class EncryptionOptions
{
    public EncryptionAlgorithm Algorithm { get; set; } = EncryptionAlgorithm.AES256;
    public string Key { get; set; }
    public string Salt { get; set; }
    public KeyDerivationFunction KeyDerivation { get; set; } = KeyDerivationFunction.PBKDF2;
    public int Iterations { get; set; } = 100000;
}

public class HashingOptions
{
    public HashingAlgorithm Algorithm { get; set; } = HashingAlgorithm.BCrypt;
    public int WorkFactor { get; set; } = 12;
    public int SaltSize { get; set; } = 16;
    public int HashSize { get; set; } = 32;
}

public enum EncryptionAlgorithm
{
    AES128,
    AES192,
    AES256
}

public enum HashingAlgorithm
{
    BCrypt,
    Argon2,
    SCrypt
}
```

## API Reference

### Extension Methods

```csharp
public static class ConveyExtensions
{
    public static IConveyBuilder AddSecurity(this IConveyBuilder builder, Action<SecurityOptions> configure = null);
    public static IConveyBuilder AddPasswordHasher<T>(this IConveyBuilder builder) where T : class, IPasswordHasher;
    public static IConveyBuilder AddEncryption(this IConveyBuilder builder);
    public static IConveyBuilder AddTokenService(this IConveyBuilder builder);
}
```

## Best Practices

1. **Use strong encryption** - Use AES-256 with proper key management
2. **Hash passwords securely** - Use Argon2 or BCrypt with appropriate work factors
3. **Validate all inputs** - Sanitize and validate user inputs
4. **Implement proper token management** - Use short-lived access tokens with refresh tokens
5. **Log security events** - Monitor and audit security-related activities
6. **Regular security updates** - Keep security libraries up to date
7. **Use secure random generation** - Use cryptographically secure random number generators
8. **Implement defense in depth** - Use multiple layers of security controls

## Troubleshooting

### Common Issues

1. **Encryption/Decryption failures**
   - Check encryption key configuration
   - Verify salt and iteration settings
   - Ensure proper encoding/decoding

2. **Password hashing issues**
   - Verify work factor configuration
   - Check for proper exception handling
   - Ensure consistent hashing algorithm

3. **Token validation failures**
   - Check token secret configuration
   - Verify issuer and audience settings
   - Ensure proper token format

4. **Input validation errors**
   - Review validation rules configuration
   - Check regex patterns
   - Verify error handling

