using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Shouldly;
using Xunit;

namespace Convey.Auth.Tests;

public class DisabledAuthenticationPolicyEvaluatorTests
{
    [Fact]
    public async Task AuthenticateAsync_Should_Return_Success_Result()
    {
        // Arrange
        var evaluator = new DisabledAuthenticationPolicyEvaluator();
        var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        var context = new DefaultHttpContext();

        // Act
        var result = await evaluator.AuthenticateAsync(policy, context);

        // Assert
        result.ShouldNotBeNull();
        result.Succeeded.ShouldBeTrue();
        result.Ticket.ShouldNotBeNull();
        result.Ticket.AuthenticationScheme.ShouldBe(JwtBearerDefaults.AuthenticationScheme);
    }

    [Fact]
    public async Task AuthenticateAsync_Should_Create_ClaimsPrincipal()
    {
        // Arrange
        var evaluator = new DisabledAuthenticationPolicyEvaluator();
        var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        var context = new DefaultHttpContext();

        // Act
        var result = await evaluator.AuthenticateAsync(policy, context);

        // Assert
        result.Principal.ShouldNotBeNull();
    }

    [Fact]
    public async Task AuthenticateAsync_Should_Create_AuthenticationTicket()
    {
        // Arrange
        var evaluator = new DisabledAuthenticationPolicyEvaluator();
        var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        var context = new DefaultHttpContext();

        // Act
        var result = await evaluator.AuthenticateAsync(policy, context);

        // Assert
        result.Ticket.ShouldNotBeNull();
        result.Ticket.Principal.ShouldNotBeNull();
        result.Ticket.Properties.ShouldNotBeNull();
    }

    [Fact]
    public async Task AuthorizeAsync_Should_Return_Success_Result()
    {
        // Arrange
        var evaluator = new DisabledAuthenticationPolicyEvaluator();
        var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        var context = new DefaultHttpContext();
        var authenticateResult = await evaluator.AuthenticateAsync(policy, context);

        // Act
        var result = await evaluator.AuthorizeAsync(policy, authenticateResult, context, null);

        // Assert
        result.ShouldNotBeNull();
        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task AuthorizeAsync_Should_Work_With_Null_Resource()
    {
        // Arrange
        var evaluator = new DisabledAuthenticationPolicyEvaluator();
        var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        var context = new DefaultHttpContext();
        var authenticateResult = await evaluator.AuthenticateAsync(policy, context);

        // Act
        var result = await evaluator.AuthorizeAsync(policy, authenticateResult, context, null);

        // Assert
        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task AuthorizeAsync_Should_Work_With_Non_Null_Resource()
    {
        // Arrange
        var evaluator = new DisabledAuthenticationPolicyEvaluator();
        var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        var context = new DefaultHttpContext();
        var authenticateResult = await evaluator.AuthenticateAsync(policy, context);
        var resource = new object();

        // Act
        var result = await evaluator.AuthorizeAsync(policy, authenticateResult, context, resource);

        // Assert
        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task AuthorizeAsync_Should_Succeed_Regardless_Of_Policy()
    {
        // Arrange
        var evaluator = new DisabledAuthenticationPolicyEvaluator();
        var strictPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .RequireRole("Admin")
            .RequireClaim("Permission", "CanDelete")
            .Build();
        var context = new DefaultHttpContext();
        var authenticateResult = await evaluator.AuthenticateAsync(strictPolicy, context);

        // Act
        var result = await evaluator.AuthorizeAsync(strictPolicy, authenticateResult, context, null);

        // Assert
        result.Succeeded.ShouldBeTrue();
    }
}
