using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using Convey.Auth.Handlers;
using Microsoft.IdentityModel.Tokens;
using Shouldly;
using Xunit;

namespace Convey.Auth.Tests;

public class JwtHandlerTests
{
    private readonly JwtOptions _jwtOptions;
    private readonly TokenValidationParameters _tokenValidationParameters;

    public JwtHandlerTests()
    {
        var issuerSigningKey = "test_secret_key_for_jwt_signing_1234567890_must_be_long_enough";
        _jwtOptions = new JwtOptions
        {
            Algorithm = SecurityAlgorithms.HmacSha256,
            Issuer = "test_issuer",
            ExpiryMinutes = 60
        };

        _tokenValidationParameters = new TokenValidationParameters
        {
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(issuerSigningKey)),
            ValidateAudience = false,
            ValidateIssuer = false,
            ValidateLifetime = false
        };
    }

    [Fact]
    public void Constructor_Should_Throw_When_IssuerSigningKey_Is_Null()
    {
        // Arrange
        var invalidParams = new TokenValidationParameters { IssuerSigningKey = null };

        // Act & Assert
        Should.Throw<InvalidOperationException>(() =>
            new JwtHandler(_jwtOptions, invalidParams))
            .Message.ShouldBe("Issuer signing key not set.");
    }

    [Fact]
    public void Constructor_Should_Throw_When_Algorithm_Is_Empty()
    {
        // Arrange
        var options = new JwtOptions { Algorithm = null };

        // Act & Assert
        Should.Throw<InvalidOperationException>(() =>
            new JwtHandler(options, _tokenValidationParameters))
            .Message.ShouldBe("Security algorithm not set.");
    }

    [Fact]
    public void Constructor_Should_Throw_When_Algorithm_Is_Whitespace()
    {
        // Arrange
        var options = new JwtOptions { Algorithm = "   " };

        // Act & Assert
        Should.Throw<InvalidOperationException>(() =>
            new JwtHandler(options, _tokenValidationParameters))
            .Message.ShouldBe("Security algorithm not set.");
    }

    [Fact]
    public void CreateToken_Should_Throw_When_UserId_Is_Empty()
    {
        // Arrange
        var handler = new JwtHandler(_jwtOptions, _tokenValidationParameters);

        // Act & Assert
        Should.Throw<ArgumentException>(() => handler.CreateToken(""))
            .Message.ShouldContain("User ID claim (subject) cannot be empty.");
    }

    [Fact]
    public void CreateToken_Should_Throw_When_UserId_Is_Null()
    {
        // Arrange
        var handler = new JwtHandler(_jwtOptions, _tokenValidationParameters);

        // Act & Assert
        Should.Throw<ArgumentException>(() => handler.CreateToken(null))
            .Message.ShouldContain("User ID claim (subject) cannot be empty.");
    }

    [Fact]
    public void CreateToken_Should_Throw_When_UserId_Is_Whitespace()
    {
        // Arrange
        var handler = new JwtHandler(_jwtOptions, _tokenValidationParameters);

        // Act & Assert
        Should.Throw<ArgumentException>(() => handler.CreateToken("   "))
            .Message.ShouldContain("User ID claim (subject) cannot be empty.");
    }

    [Fact]
    public void CreateToken_Should_Create_Valid_Token_With_UserId()
    {
        // Arrange
        var handler = new JwtHandler(_jwtOptions, _tokenValidationParameters);
        var userId = "user123";

        // Act
        var token = handler.CreateToken(userId);

        // Assert
        token.ShouldNotBeNull();
        token.AccessToken.ShouldNotBeNullOrEmpty();
        token.Id.ShouldBe(userId);
        token.RefreshToken.ShouldBe(string.Empty);
        token.Expires.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void CreateToken_Should_Include_Role_When_Provided()
    {
        // Arrange
        var handler = new JwtHandler(_jwtOptions, _tokenValidationParameters);
        var userId = "user123";
        var role = "admin";

        // Act
        var token = handler.CreateToken(userId, role);

        // Assert
        token.Role.ShouldBe(role);
    }

    [Fact]
    public void CreateToken_Should_Handle_Empty_Role()
    {
        // Arrange
        var handler = new JwtHandler(_jwtOptions, _tokenValidationParameters);

        // Act
        var token = handler.CreateToken("user123", role: "");

        // Assert
        token.Role.ShouldBe(string.Empty);
    }

    [Fact]
    public void CreateToken_Should_Handle_Null_Role()
    {
        // Arrange
        var handler = new JwtHandler(_jwtOptions, _tokenValidationParameters);

        // Act
        var token = handler.CreateToken("user123", role: null);

        // Assert
        token.Role.ShouldBe(string.Empty);
    }

    [Fact]
    public void CreateToken_Should_Include_Custom_Claims()
    {
        // Arrange
        var handler = new JwtHandler(_jwtOptions, _tokenValidationParameters);
        var userId = "user123";
        var claims = new Dictionary<string, IEnumerable<string>>
        {
            { "custom_claim", new[] { "value1", "value2" } }
        };

        // Act
        var token = handler.CreateToken(userId, claims: claims);

        // Assert
        token.Claims.ShouldContain(x => x.Key == "custom_claim");
        token.Claims["custom_claim"].ShouldContain("value1");
        token.Claims["custom_claim"].ShouldContain("value2");
    }

    [Fact]
    public void CreateToken_Should_Handle_Null_Claims()
    {
        // Arrange
        var handler = new JwtHandler(_jwtOptions, _tokenValidationParameters);

        // Act
        var token = handler.CreateToken("user123", claims: null);

        // Assert
        token.Claims.ShouldNotBeNull();
        token.Claims.Count.ShouldBe(0);
    }

    [Fact]
    public void CreateToken_Should_Handle_Empty_Claims_Dictionary()
    {
        // Arrange
        var handler = new JwtHandler(_jwtOptions, _tokenValidationParameters);
        var claims = new Dictionary<string, IEnumerable<string>>();

        // Act
        var token = handler.CreateToken("user123", claims: claims);

        // Assert
        token.Claims.ShouldNotBeNull();
        token.Claims.Count.ShouldBe(0);
    }

    [Fact]
    public void CreateToken_Should_Use_Expiry_TimeSpan_When_Set()
    {
        // Arrange
        _jwtOptions.Expiry = TimeSpan.FromMinutes(30);
        var handler = new JwtHandler(_jwtOptions, _tokenValidationParameters);

        // Act
        var token = handler.CreateToken("user123");

        // Assert
        token.Expires.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void CreateToken_Should_Include_Audience_When_Provided()
    {
        // Arrange
        var handler = new JwtHandler(_jwtOptions, _tokenValidationParameters);
        var audience = "test_audience";

        // Act
        var token = handler.CreateToken("user123", audience: audience);

        // Assert
        token.ShouldNotBeNull();
        var jwtHandler = new JwtSecurityTokenHandler();
        var jwtToken = jwtHandler.ReadJwtToken(token.AccessToken);
        jwtToken.Audiences.ShouldContain(audience);
    }

    [Fact]
    public void GetTokenPayload_Should_Return_Payload_For_Valid_Token()
    {
        // Arrange
        var handler = new JwtHandler(_jwtOptions, _tokenValidationParameters);
        var userId = "user123";
        var role = "admin";
        var token = handler.CreateToken(userId, role);

        // Act
        var payload = handler.GetTokenPayload(token.AccessToken);

        // Assert
        payload.ShouldNotBeNull();
        payload.Subject.ShouldBe(userId);
        payload.Role.ShouldBe(role);
        payload.Expires.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void GetTokenPayload_Should_Include_Custom_Claims()
    {
        // Arrange
        var handler = new JwtHandler(_jwtOptions, _tokenValidationParameters);
        var userId = "user123";
        var customClaims = new Dictionary<string, IEnumerable<string>>
        {
            { "department", new[] { "engineering" } },
            { "permissions", new[] { "read", "write", "delete" } }
        };
        var token = handler.CreateToken(userId, claims: customClaims);

        // Act
        var payload = handler.GetTokenPayload(token.AccessToken);

        // Assert
        payload.Claims.ShouldContainKey("department");
        payload.Claims["department"].ShouldContain("engineering");
        payload.Claims.ShouldContainKey("permissions");
        payload.Claims["permissions"].Count().ShouldBe(3);
    }

    [Fact]
    public void GetTokenPayload_Should_Throw_For_Invalid_Token()
    {
        // Arrange
        var handler = new JwtHandler(_jwtOptions, _tokenValidationParameters);

        // Act & Assert
        Should.Throw<Exception>(() => handler.GetTokenPayload("invalid_token"));
    }

    [Fact]
    public void GetTokenPayload_Should_Return_Null_For_Non_JWT_Token()
    {
        // Arrange
        var handler = new JwtHandler(_jwtOptions, _tokenValidationParameters);

        // Act & Assert
        Should.Throw<ArgumentException>(() => handler.GetTokenPayload("not.a.jwt"));
    }

    [Fact]
    public void CreateToken_Should_Create_Readable_JWT()
    {
        // Arrange
        var handler = new JwtHandler(_jwtOptions, _tokenValidationParameters);
        var userId = "user123";

        // Act
        var token = handler.CreateToken(userId);

        // Assert
        var jwtHandler = new JwtSecurityTokenHandler();
        var canRead = jwtHandler.CanReadToken(token.AccessToken);
        canRead.ShouldBeTrue();
    }

    [Fact]
    public void CreateToken_Should_Include_Standard_Claims()
    {
        // Arrange
        var handler = new JwtHandler(_jwtOptions, _tokenValidationParameters);
        var userId = "user123";

        // Act
        var token = handler.CreateToken(userId);

        // Assert
        var jwtHandler = new JwtSecurityTokenHandler();
        var jwtToken = jwtHandler.ReadJwtToken(token.AccessToken);

        jwtToken.Claims.ShouldContain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == userId);
        jwtToken.Claims.ShouldContain(c => c.Type == JwtRegisteredClaimNames.UniqueName && c.Value == userId);
        jwtToken.Claims.ShouldContain(c => c.Type == JwtRegisteredClaimNames.Jti);
        jwtToken.Claims.ShouldContain(c => c.Type == JwtRegisteredClaimNames.Iat);
    }

    [Fact]
    public void CreateToken_With_Multiple_Values_For_Same_Claim()
    {
        // Arrange
        var handler = new JwtHandler(_jwtOptions, _tokenValidationParameters);
        var claims = new Dictionary<string, IEnumerable<string>>
        {
            { "scope", new[] { "read", "write", "admin" } }
        };

        // Act
        var token = handler.CreateToken("user123", claims: claims);

        // Assert
        token.Claims["scope"].Count().ShouldBe(3);
        token.Claims["scope"].ShouldContain("read");
        token.Claims["scope"].ShouldContain("write");
        token.Claims["scope"].ShouldContain("admin");
    }

    [Fact]
    public void CreateToken_Should_Use_ExpiryMinutes_When_Expiry_Is_Null()
    {
        // Arrange
        _jwtOptions.Expiry = null;
        _jwtOptions.ExpiryMinutes = 120;
        var handler = new JwtHandler(_jwtOptions, _tokenValidationParameters);

        // Act
        var token = handler.CreateToken("user123");

        // Assert
        var jwtHandler = new JwtSecurityTokenHandler();
        var jwtToken = jwtHandler.ReadJwtToken(token.AccessToken);

        // Allow some buffer for execution time
        var expectedExpiry = DateTime.UtcNow.AddMinutes(120);
        var diff = (jwtToken.ValidTo - expectedExpiry).Duration();
        diff.TotalSeconds.ShouldBeLessThan(5);
    }
}
