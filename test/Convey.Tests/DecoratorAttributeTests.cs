using Convey.Types;
using Shouldly;
using Xunit;

namespace Convey.Tests;

public class DecoratorAttributeTests
{
    [Fact]
    public void Attribute_Should_Be_Applicable_To_Classes()
    {
        // Arrange & Act
        var attribute = new DecoratorAttribute();

        // Assert
        attribute.ShouldNotBeNull();
        attribute.ShouldBeOfType<DecoratorAttribute>();
    }

    [Fact]
    public void Attribute_Should_Be_Applied_To_Test_Class()
    {
        // Arrange & Act
        var attributes = typeof(TestDecoratedClass).GetCustomAttributes(typeof(DecoratorAttribute), false);

        // Assert
        attributes.Length.ShouldBe(1);
        attributes[0].ShouldBeOfType<DecoratorAttribute>();
    }

    [Fact]
    public void Attribute_Should_Be_Inherited()
    {
        // Arrange & Act
        var attributes = typeof(InheritedDecoratedClass).GetCustomAttributes(typeof(DecoratorAttribute), true);

        // Assert
        attributes.Length.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void Attribute_Should_Not_Allow_Multiple()
    {
        // Arrange & Act
        var attributeUsage = typeof(DecoratorAttribute)
            .GetCustomAttributes(typeof(System.AttributeUsageAttribute), false)[0]
            as System.AttributeUsageAttribute;

        // Assert
        attributeUsage.ShouldNotBeNull();
        attributeUsage.AllowMultiple.ShouldBeFalse();
    }

    [Fact]
    public void Attribute_Should_Target_Classes()
    {
        // Arrange & Act
        var attributeUsage = typeof(DecoratorAttribute)
            .GetCustomAttributes(typeof(System.AttributeUsageAttribute), false)[0]
            as System.AttributeUsageAttribute;

        // Assert
        attributeUsage.ShouldNotBeNull();
        attributeUsage.ValidOn.ShouldBe(System.AttributeTargets.Class);
    }

    // Test helper classes
    [Decorator]
    private class TestDecoratedClass
    {
    }

    private class InheritedDecoratedClass : TestDecoratedClass
    {
    }
}
