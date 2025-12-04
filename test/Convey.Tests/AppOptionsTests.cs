using Convey.Types;
using Shouldly;
using Xunit;

namespace Convey.Tests;

public class AppOptionsTests
{
    [Fact]
    public void Constructor_Should_Set_Default_Values()
    {
        // Arrange & Act
        var options = new AppOptions();

        // Assert
        options.DisplayBanner.ShouldBeTrue();
        options.DisplayVersion.ShouldBeTrue();
        options.Name.ShouldBeNull();
        options.Service.ShouldBeNull();
        options.Instance.ShouldBeNull();
        options.Version.ShouldBeNull();
    }

    [Fact]
    public void Properties_Should_Be_Settable()
    {
        // Arrange
        var options = new AppOptions();

        // Act
        options.Name = "TestApp";
        options.Service = "TestService";
        options.Instance = "Instance1";
        options.Version = "1.0.0";
        options.DisplayBanner = false;
        options.DisplayVersion = false;

        // Assert
        options.Name.ShouldBe("TestApp");
        options.Service.ShouldBe("TestService");
        options.Instance.ShouldBe("Instance1");
        options.Version.ShouldBe("1.0.0");
        options.DisplayBanner.ShouldBeFalse();
        options.DisplayVersion.ShouldBeFalse();
    }

    [Fact]
    public void Properties_Should_Accept_Null_Values()
    {
        // Arrange
        var options = new AppOptions
        {
            Name = "Test",
            Service = "Service",
            Instance = "Instance",
            Version = "1.0"
        };

        // Act
        options.Name = null;
        options.Service = null;
        options.Instance = null;
        options.Version = null;

        // Assert
        options.Name.ShouldBeNull();
        options.Service.ShouldBeNull();
        options.Instance.ShouldBeNull();
        options.Version.ShouldBeNull();
    }

    [Fact]
    public void Properties_Should_Accept_Empty_Strings()
    {
        // Arrange
        var options = new AppOptions();

        // Act
        options.Name = "";
        options.Service = "";
        options.Instance = "";
        options.Version = "";

        // Assert
        options.Name.ShouldBe("");
        options.Service.ShouldBe("");
        options.Instance.ShouldBe("");
        options.Version.ShouldBe("");
    }
}
