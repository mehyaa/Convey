using System;
using System.Threading.Tasks;
using Convey.Auth.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Shouldly;
using Xunit;

namespace Convey.Auth.Tests;

public class InMemoryAccessTokenServiceTests
{
    private readonly Mock<IMemoryCache> _cacheMock;
    private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock;
    private readonly JwtOptions _jwtOptions;

    public InMemoryAccessTokenServiceTests()
    {
        _cacheMock = new Mock<IMemoryCache>();
        _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        _jwtOptions = new JwtOptions { ExpiryMinutes = 60 };
    }

    [Fact]
    public async Task IsActiveAsync_Should_Return_True_When_Token_Not_Blacklisted()
    {
        // Arrange
        object cacheValue = null;
        _cacheMock.Setup(x => x.TryGetValue(It.IsAny<object>(), out cacheValue))
            .Returns(false);

        var service = new InMemoryAccessTokenService(
            _cacheMock.Object,
            _httpContextAccessorMock.Object,
            _jwtOptions);

        // Act
        var result = await service.IsTokenActiveAsync("test_token");

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task IsActiveAsync_Should_Return_False_When_Token_Is_Blacklisted()
    {
        // Arrange
        var cacheEntryMock = new Mock<ICacheEntry>();
        object cacheValue = "revoked";

        _cacheMock.Setup(x => x.TryGetValue(It.IsAny<object>(), out cacheValue))
            .Returns(true);
        _cacheMock.Setup(x => x.CreateEntry(It.IsAny<object>()))
            .Returns(cacheEntryMock.Object);

        var service = new InMemoryAccessTokenService(
            _cacheMock.Object,
            _httpContextAccessorMock.Object,
            _jwtOptions);

        // Act
        var result = await service.IsTokenActiveAsync("test_token");

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task DeactivateAsync_Should_Add_Token_To_Cache()
    {
        // Arrange
        var cacheEntryMock = new Mock<ICacheEntry>();
        cacheEntryMock.SetupSet(x => x.AbsoluteExpirationRelativeToNow = It.IsAny<TimeSpan>()).Verifiable();
        cacheEntryMock.SetupSet(x => x.Value = It.IsAny<object>()).Verifiable();

        _cacheMock.Setup(x => x.CreateEntry(It.IsAny<object>()))
            .Returns(cacheEntryMock.Object);

        var service = new InMemoryAccessTokenService(
            _cacheMock.Object,
            _httpContextAccessorMock.Object,
            _jwtOptions);

        // Act
        await service.DeactivateTokenAsync("test_token");

        // Assert
        _cacheMock.Verify(x => x.CreateEntry(It.Is<string>(k => k.Contains("test_token"))), Times.Once);
        cacheEntryMock.VerifySet(x => x.AbsoluteExpirationRelativeToNow = It.IsAny<TimeSpan>(), Times.Once);
    }

    [Fact]
    public async Task IsCurrentActiveToken_Should_Return_True_For_Active_Token()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["authorization"] = "Bearer active_token";
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);

        object cacheValue = null;
        _cacheMock.Setup(x => x.TryGetValue(It.IsAny<object>(), out cacheValue))
            .Returns(false);

        var service = new InMemoryAccessTokenService(
            _cacheMock.Object,
            _httpContextAccessorMock.Object,
            _jwtOptions);

        // Act
        var result = await service.IsTokenActiveAsync();

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task IsCurrentActiveToken_Should_Handle_Empty_Authorization_Header()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);

        object cacheValue = null;
        _cacheMock.Setup(x => x.TryGetValue(It.IsAny<object>(), out cacheValue))
            .Returns(false);

        var service = new InMemoryAccessTokenService(
            _cacheMock.Object,
            _httpContextAccessorMock.Object,
            _jwtOptions);

        // Act
        var result = await service.IsTokenActiveAsync();

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task DeactivateCurrentAsync_Should_Deactivate_Current_Token()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["authorization"] = "Bearer current_token";
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);

        var cacheEntryMock = new Mock<ICacheEntry>();
        _cacheMock.Setup(x => x.CreateEntry(It.IsAny<object>()))
            .Returns(cacheEntryMock.Object);

        var service = new InMemoryAccessTokenService(
            _cacheMock.Object,
            _httpContextAccessorMock.Object,
            _jwtOptions);

        // Act
        await service.DeactivateTokenAsync();

        // Assert
        _cacheMock.Verify(x => x.CreateEntry(It.Is<string>(k => k.Contains("current_token"))), Times.Once);
    }

    [Fact]
    public void Constructor_Should_Use_Expiry_TimeSpan_When_Set()
    {
        // Arrange
        _jwtOptions.Expiry = TimeSpan.FromMinutes(30);

        // Act
        var service = new InMemoryAccessTokenService(
            _cacheMock.Object,
            _httpContextAccessorMock.Object,
            _jwtOptions);

        // Assert
        service.ShouldNotBeNull();
    }

    [Fact]
    public void Constructor_Should_Use_ExpiryMinutes_When_Expiry_Is_Null()
    {
        // Arrange
        _jwtOptions.Expiry = null;
        _jwtOptions.ExpiryMinutes = 120;

        // Act
        var service = new InMemoryAccessTokenService(
            _cacheMock.Object,
            _httpContextAccessorMock.Object,
            _jwtOptions);

        // Assert
        service.ShouldNotBeNull();
    }

    [Fact]
    public async Task DeactivateAsync_Should_Set_Cache_Expiration()
    {
        // Arrange
        var expectedExpiry = TimeSpan.FromMinutes(60);
        _jwtOptions.ExpiryMinutes = 60;

        var cacheEntryMock = new Mock<ICacheEntry>();
        TimeSpan? capturedExpiry = null;
        cacheEntryMock.SetupSet(x => x.AbsoluteExpirationRelativeToNow = It.IsAny<TimeSpan>())
            .Callback<TimeSpan?>((value) => capturedExpiry = value);

        _cacheMock.Setup(x => x.CreateEntry(It.IsAny<object>()))
            .Returns(cacheEntryMock.Object);

        var service = new InMemoryAccessTokenService(
            _cacheMock.Object,
            _httpContextAccessorMock.Object,
            _jwtOptions);

        // Act
        await service.DeactivateTokenAsync("token");

        // Assert
        capturedExpiry.ShouldBe(expectedExpiry);
    }
}
