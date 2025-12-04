using Shouldly;
using Xunit;

namespace Convey.Auth.Tests;

public class JwtAuthAttributeTests
{
    [Fact]
    public void Constructor_Should_Use_Bearer_Scheme_By_Default()
    {
        // Arrange & Act
        var attribute = new JwtAuthAttribute();

        // Assert
        attribute.AuthenticationSchemes.ShouldBe("Bearer");
    }

    [Fact]
    public void Constructor_Should_Set_Policy_When_Provided()
    {
        // Arrange & Act
        var attribute = new JwtAuthAttribute("AdminPolicy");

        // Assert
        attribute.Policy.ShouldBe("AdminPolicy");
        attribute.AuthenticationSchemes.ShouldBe("Bearer");
    }

    [Fact]
    public void Constructor_Should_Set_Empty_Policy_When_Not_Provided()
    {
        // Arrange & Act
        var attribute = new JwtAuthAttribute();

        // Assert
        attribute.Policy.ShouldBe("");
    }

    [Fact]
    public void Should_Inherit_From_AuthAttribute()
    {
        // Arrange
        var attribute = new JwtAuthAttribute();

        // Act & Assert
        attribute.ShouldBeAssignableTo<AuthAttribute>();
    }

    [Fact]
    public void AuthenticationScheme_Constant_Should_Be_Bearer()
    {
        // Assert
        JwtAuthAttribute.AuthenticationScheme.ShouldBe("Bearer");
    }
}
