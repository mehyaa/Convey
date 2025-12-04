using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Moq;
using Shouldly;
using Xunit;

namespace Convey.Auth.Tests;

public class AccessTokenValidatorMiddlewareTests
{
    private readonly Mock<IAccessTokenService> _accessTokenServiceMock;
    private readonly JwtOptions _jwtOptions;
    private readonly AccessTokenValidatorMiddleware _middleware;

    public AccessTokenValidatorMiddlewareTests()
    {
        _accessTokenServiceMock = new Mock<IAccessTokenService>();
        _jwtOptions = new JwtOptions();
        _middleware = new AccessTokenValidatorMiddleware(_accessTokenServiceMock.Object, _jwtOptions);
    }

    [Fact]
    public async Task InvokeAsync_Should_Call_Next_When_Endpoint_Is_Allowed()
    {
        // Arrange
        _jwtOptions.AllowAnonymousEndpoints = new List<string> { "/allowed" };
        // Re-instantiate because options are passed in constructor
        var middleware = new AccessTokenValidatorMiddleware(_accessTokenServiceMock.Object, _jwtOptions);

        var context = new DefaultHttpContext();
        context.Request.Path = "/allowed";
        var nextCalled = false;
        RequestDelegate next = (ctx) => { nextCalled = true; return Task.CompletedTask; };

        // Act
        await middleware.InvokeAsync(context, next);

        // Assert
        nextCalled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(200); // Default
        _accessTokenServiceMock.Verify(x => x.IsTokenActiveAsync(), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_Should_Call_Next_When_Token_Is_Active()
    {
        // Arrange
        _accessTokenServiceMock.Setup(x => x.IsTokenActiveAsync()).ReturnsAsync(true);
        var context = new DefaultHttpContext();
        context.Request.Path = "/protected";
        var nextCalled = false;
        RequestDelegate next = (ctx) => { nextCalled = true; return Task.CompletedTask; };

        // Act
        await _middleware.InvokeAsync(context, next);

        // Assert
        nextCalled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(200);
    }

    [Fact]
    public async Task InvokeAsync_Should_Return_Unauthorized_When_Token_Is_Not_Active()
    {
        // Arrange
        _accessTokenServiceMock.Setup(x => x.IsTokenActiveAsync()).ReturnsAsync(false);
        var context = new DefaultHttpContext();
        context.Request.Path = "/protected";
        var nextCalled = false;
        RequestDelegate next = (ctx) => { nextCalled = true; return Task.CompletedTask; };

        // Act
        await _middleware.InvokeAsync(context, next);

        // Assert
        nextCalled.ShouldBeFalse();
        context.Response.StatusCode.ShouldBe(401);
    }
}
