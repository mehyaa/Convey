using Convey.Auth.Builders;
using Shouldly;
using Xunit;

namespace Convey.Auth.Tests;

public class JwtOptionsBuilderTests
{
    [Fact]
    public void Build_Should_Return_JwtOptions_Instance()
    {
        // Arrange
        var builder = new JwtOptionsBuilder();

        // Act
        var options = builder.Build();

        // Assert
        options.ShouldNotBeNull();
        options.ShouldBeOfType<JwtOptions>();
    }

    [Fact]
    public void WithIssuerSigningKey_Should_Set_IssuerSigningKey()
    {
        // Arrange
        var builder = new JwtOptionsBuilder();
        var key = "test_signing_key";

        // Act
        var options = builder.WithIssuerSigningKey(key).Build();

        // Assert
        options.IssuerSigningKey.ShouldBe(key);
    }

    [Fact]
    public void WithIssuer_Should_Set_ValidIssuer()
    {
        // Arrange
        var builder = new JwtOptionsBuilder();
        var issuer = "test_issuer";

        // Act
        var options = builder.WithIssuer(issuer).Build();

        // Assert
        options.ValidIssuer.ShouldBe(issuer);
    }

    [Fact]
    public void WithExpiryMinutes_Should_Set_ExpiryMinutes()
    {
        // Arrange
        var builder = new JwtOptionsBuilder();
        var expiryMinutes = 60;

        // Act
        var options = builder.WithExpiryMinutes(expiryMinutes).Build();

        // Assert
        options.ExpiryMinutes.ShouldBe(expiryMinutes);
    }

    [Fact]
    public void WithLifetimeValidation_Should_Set_ValidateLifetime_True()
    {
        // Arrange
        var builder = new JwtOptionsBuilder();

        // Act
        var options = builder.WithLifetimeValidation(true).Build();

        // Assert
        options.ValidateLifetime.ShouldBeTrue();
    }

    [Fact]
    public void WithLifetimeValidation_Should_Set_ValidateLifetime_False()
    {
        // Arrange
        var builder = new JwtOptionsBuilder();

        // Act
        var options = builder.WithLifetimeValidation(false).Build();

        // Assert
        options.ValidateLifetime.ShouldBeFalse();
    }

    [Fact]
    public void WithAudienceValidation_Should_Set_ValidateAudience_True()
    {
        // Arrange
        var builder = new JwtOptionsBuilder();

        // Act
        var options = builder.WithAudienceValidation(true).Build();

        // Assert
        options.ValidateAudience.ShouldBeTrue();
    }

    [Fact]
    public void WithAudienceValidation_Should_Set_ValidateAudience_False()
    {
        // Arrange
        var builder = new JwtOptionsBuilder();

        // Act
        var options = builder.WithAudienceValidation(false).Build();

        // Assert
        options.ValidateAudience.ShouldBeFalse();
    }

    [Fact]
    public void WithValidAudience_Should_Set_ValidAudience()
    {
        // Arrange
        var builder = new JwtOptionsBuilder();
        var audience = "test_audience";

        // Act
        var options = builder.WithValidAudience(audience).Build();

        // Assert
        options.ValidAudience.ShouldBe(audience);
    }

    [Fact]
    public void Builder_Should_Support_Fluent_Chaining()
    {
        // Arrange
        var builder = new JwtOptionsBuilder();

        // Act
        var options = builder
            .WithIssuerSigningKey("key")
            .WithIssuer("issuer")
            .WithExpiryMinutes(120)
            .WithLifetimeValidation(true)
            .WithAudienceValidation(true)
            .WithValidAudience("audience")
            .Build();

        // Assert
        options.IssuerSigningKey.ShouldBe("key");
        options.ValidIssuer.ShouldBe("issuer");
        options.ExpiryMinutes.ShouldBe(120);
        options.ValidateLifetime.ShouldBeTrue();
        options.ValidateAudience.ShouldBeTrue();
        options.ValidAudience.ShouldBe("audience");
    }

    [Fact]
    public void Build_Should_Return_New_Options_With_Default_Values()
    {
        // Arrange
        var builder = new JwtOptionsBuilder();

        // Act
        var options = builder.Build();

        // Assert
        options.SaveToken.ShouldBeTrue();
        options.RequireAudience.ShouldBeTrue();
        options.ValidateAudience.ShouldBeTrue();
        options.ValidateIssuer.ShouldBeTrue();
        options.ValidateLifetime.ShouldBeTrue();
    }

    [Fact]
    public void Multiple_Builds_Should_Return_Same_Instance()
    {
        // Arrange
        var builder = new JwtOptionsBuilder();

        // Act
        var options1 = builder.Build();
        var options2 = builder.Build();

        // Assert
        options1.ShouldBeSameAs(options2);
    }
}
