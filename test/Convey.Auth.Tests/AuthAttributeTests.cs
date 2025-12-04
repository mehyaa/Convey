using Shouldly;
using Xunit;

namespace Convey.Auth.Tests;

public class AuthAttributeTests
{
    [Fact]
    public void Constructor_Should_Set_AuthenticationSchemes()
    {
        // Arrange & Act
        var attribute = new AuthAttribute("TestScheme");

        // Assert
        attribute.AuthenticationSchemes.ShouldBe("TestScheme");
    }

    [Fact]
    public void Constructor_Should_Set_Policy_When_Provided()
    {
        // Arrange & Act
        var attribute = new AuthAttribute("TestScheme", "TestPolicy");

        // Assert
        attribute.Policy.ShouldBe("TestPolicy");
        attribute.AuthenticationSchemes.ShouldBe("TestScheme");
    }

    [Fact]
    public void Constructor_Should_Set_Empty_Policy_When_Not_Provided()
    {
        // Arrange & Act
        var attribute = new AuthAttribute("TestScheme");

        // Assert
        attribute.Policy.ShouldBe("");
    }

    [Fact]
    public void Should_Be_AuthorizeAttribute()
    {
        // Arrange
        var attribute = new AuthAttribute("TestScheme");

        // Act & Assert
        attribute.ShouldBeAssignableTo<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>();
    }
}
