using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Moq;
using Shouldly;
using Xunit;
using Microsoft.IdentityModel.Tokens;

namespace Convey.Auth.Tests;

public class ExtensionsTests
{
    [Fact]
    public void AddJwt_Should_Register_Services()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                {"jwt:issuer", "test_issuer"},
                {"jwt:issuerSigningKey", "secret_key_1234567890_secret_key_1234567890"}
            })
            .Build();

        var builder = ConveyBuilder.Create(services, configuration);

        builder.AddJwt();

        var provider = services.BuildServiceProvider();
        var options = provider.GetService<JwtOptions>();

        options.ShouldNotBeNull();
        options.Issuer.ShouldBe("test_issuer");
    }
    [Fact]
    public void AddJwt_Should_Register_DisabledAuthenticationPolicyEvaluator_When_AuthenticationDisabled_Is_True()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                {"jwt:authenticationDisabled", "true"}
            })
            .Build();

        var builder = ConveyBuilder.Create(services, configuration);

        builder.AddJwt();

        var provider = services.BuildServiceProvider();
        var options = provider.GetService<JwtOptions>();
        var policyEvaluator = provider.GetService<IPolicyEvaluator>();

        options.ShouldNotBeNull();
        options.AuthenticationDisabled.ShouldBeTrue();
        policyEvaluator.ShouldNotBeNull();
        policyEvaluator.GetType().Name.ShouldBe("DisabledAuthenticationPolicyEvaluator");
    }

    [Fact]
    public void UseAccessTokenValidator_Should_Register_Middleware()
    {
        var services = new ServiceCollection();
        var builder = ConveyBuilder.Create(services, new ConfigurationBuilder().Build());
        builder.AddJwt();

        var appBuilderMock = new Mock<IApplicationBuilder>();
        appBuilderMock.Setup(x => x.ApplicationServices).Returns(services.BuildServiceProvider());

        // Act
        appBuilderMock.Object.UseAccessTokenValidator();

        // Assert
        appBuilderMock.Verify(x => x.Use(It.IsAny<Func<RequestDelegate, RequestDelegate>>()), Times.Once);
    }

    [Fact]
    public void AddJwt_Should_Use_Symmetric_Key_When_Certificate_Is_Missing()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                {"jwt:issuerSigningKey", "secret_key_1234567890_secret_key_1234567890"}
            })
            .Build();

        var builder = ConveyBuilder.Create(services, configuration);

        // Act
        builder.AddJwt();

        // Assert
        var provider = services.BuildServiceProvider();
        var validationParams = provider.GetService<TokenValidationParameters>();
        validationParams.ShouldNotBeNull();
        validationParams.IssuerSigningKey.ShouldBeOfType<SymmetricSecurityKey>();
    }

    [Fact]
    public void AddJwt_Should_Load_Certificate_From_RawData()
    {
        // Arrange
        // Generate a self-signed certificate for testing
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var request = new System.Security.Cryptography.X509Certificates.CertificateRequest(
            new System.Security.Cryptography.X509Certificates.X500DistinguishedName("CN=TestCert"), rsa, System.Security.Cryptography.HashAlgorithmName.SHA256, System.Security.Cryptography.RSASignaturePadding.Pkcs1);
        var certificate = request.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddYears(1));
        var rawData = Convert.ToBase64String(certificate.Export(System.Security.Cryptography.X509Certificates.X509ContentType.Pfx));

        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                {"jwt:certificate:rawData", rawData},
                {"jwt:certificate:password", ""}
            })
            .Build();

        var builder = ConveyBuilder.Create(services, configuration);

        // Act
        builder.AddJwt();

        // Assert
        var provider = services.BuildServiceProvider();
        var validationParams = provider.GetService<TokenValidationParameters>();
        validationParams.ShouldNotBeNull();
        validationParams.IssuerSigningKey.ShouldBeOfType<X509SecurityKey>();
    }
}
