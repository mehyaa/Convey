using Convey.Types;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Convey.Tests;

public class ExtensionsTests
{
    [Fact]
    public void AddConvey_Should_Register_Services()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                {"app:name", "test_app"},
                {"app:displayBanner", "false"}
            })
            .Build();

        // Act
        var builder = services.AddConvey(configuration: configuration);

        // Assert
        builder.ShouldNotBeNull();
        services.ShouldContain(s => s.ServiceType == typeof(IServiceId));
        services.ShouldContain(s => s.ServiceType == typeof(AppOptions));
    }

    [Fact]
    public void AddConvey_Should_Use_Default_Section_Name_When_Null()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        // Act
        var builder = services.AddConvey(sectionName: null, configuration: configuration);

        // Assert
        builder.ShouldNotBeNull();
    }

    [Fact]
    public void AddConvey_Should_Use_Default_Section_Name_When_Empty()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        // Act
        var builder = services.AddConvey(sectionName: "", configuration: configuration);

        // Assert
        builder.ShouldNotBeNull();
    }

    [Fact]
    public void AddConvey_Should_Use_Default_Section_Name_When_Whitespace()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        // Act
        var builder = services.AddConvey(sectionName: "   ", configuration: configuration);

        // Assert
        builder.ShouldNotBeNull();
    }

    [Fact]
    public void AddConvey_Should_Register_Memory_Cache()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        // Act
        services.AddConvey(configuration: configuration);

        // Assert
        services.ShouldContain(s => s.ServiceType.Name.Contains("IMemoryCache"));
    }

    [Fact]
    public void AddConvey_Should_Register_StartupInitializer()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        // Act
        services.AddConvey(configuration: configuration);

        // Assert
        services.ShouldContain(s => s.ImplementationType == typeof(StartupInitializer));
    }

    [Fact]
    public void AddConvey_Should_Not_Display_Banner_When_DisplayBanner_Is_False()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                {"app:name", "TestApp"},
                {"app:displayBanner", "false"}
            })
            .Build();

        // Act
        var builder = services.AddConvey(configuration: configuration);

        // Assert
        builder.ShouldNotBeNull();
    }

    [Fact]
    public void AddConvey_Should_Return_Builder_When_Name_Is_Missing()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                {"app:displayBanner", "true"}
            })
            .Build();

        // Act
        var builder = services.AddConvey(configuration: configuration);

        // Assert
        builder.ShouldNotBeNull();
    }

    [Fact]
    public void AddInitializer_Generic_Should_Register_Initializer()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = ConveyBuilder.Create(services);

        // Act
        builder.AddInitializer<TestInitializer>();

        // Assert
        services.ShouldContain(s => s.ServiceType == typeof(IInitializer) &&
                                    s.ImplementationType == typeof(TestInitializer));
    }

    [Fact]
    public void AddInitializer_WithResolver_Should_Register_Initializer()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = ConveyBuilder.Create(services);

        // Act
        builder.AddInitializer<TestInitializer>(sp => new TestInitializer());

        // Assert
        services.ShouldContain(s => s.ServiceType == typeof(IInitializer));
    }

    [Fact]
    public void AddInitializer_Should_Support_Multiple_Initializers()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = ConveyBuilder.Create(services);

        // Act
        builder.AddInitializer<TestInitializer>();
        builder.AddInitializer<AnotherTestInitializer>();

        // Assert
        var initializerServices = services.Where(s => s.ServiceType == typeof(IInitializer)).ToList();
        initializerServices.Count.ShouldBe(2);
    }

    [Fact]
    public void Underscore_Should_Convert_PascalCase_To_SnakeCase()
    {
        // Arrange & Act & Assert
        "PascalCase".Underscore().ShouldBe("pascal_case");
        "camelCase".Underscore().ShouldBe("camel_case");
        "ABC".Underscore().ShouldBe("a_b_c");
    }

    [Fact]
    public void Underscore_Should_Handle_Single_Character()
    {
        // Arrange & Act & Assert
        "A".Underscore().ShouldBe("a");
        "a".Underscore().ShouldBe("a");
    }

    [Fact]
    public void Underscore_Should_Handle_Empty_String()
    {
        // Arrange & Act & Assert
        "".Underscore().ShouldBe("");
    }

    [Fact]
    public void Underscore_Should_Handle_Lower_Case()
    {
        // Arrange & Act & Assert
        "lowercase".Underscore().ShouldBe("lowercase");
    }

    [Fact]
    public void Underscore_Should_Handle_Already_Snake_Case()
    {
        // Arrange & Act & Assert
        "already_snake_case".Underscore().ShouldBe("already_snake_case");
    }

    [Fact]
    public void Underscore_Should_Handle_Numbers()
    {
        // Arrange & Act & Assert
        "Test123Value".Underscore().ShouldBe("test123_value");
    }

    [Fact]
    public void GetOptions_IConfiguration_Should_Bind_Configuration()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                {"app:name", "test_app"},
                {"app:version", "1.0"},
                {"app:service", "my_service"}
            })
            .Build();

        // Act
        var options = configuration.GetOptions<AppOptions>("app");

        // Assert
        options.ShouldNotBeNull();
        options.Name.ShouldBe("test_app");
        options.Version.ShouldBe("1.0");
        options.Service.ShouldBe("my_service");
    }

    [Fact]
    public void GetOptions_IConfiguration_Should_Return_Empty_Object_For_Missing_Section()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();

        // Act
        var options = configuration.GetOptions<AppOptions>("nonexistent");

        // Assert
        options.ShouldNotBeNull();
        options.Name.ShouldBeNull();
    }

    [Fact]
    public void GetOptions_IConveyBuilder_Should_Bind_Configuration()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                {"app:name", "test_app"},
                {"app:version", "1.0"}
            })
            .Build();
        var services = new ServiceCollection();
        var builder = ConveyBuilder.Create(services, configuration);

        // Act
        var options = builder.GetOptions<AppOptions>("app");

        // Assert
        options.ShouldNotBeNull();
        options.Name.ShouldBe("test_app");
        options.Version.ShouldBe("1.0");
    }

    [Fact]
    public void GetOptions_IConveyBuilder_Should_Use_Registered_Configuration_When_Builder_Has_None()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                {"app:name", "test_app"}
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        var builder = ConveyBuilder.Create(services, null);

        // Act
        var options = builder.GetOptions<AppOptions>("app");

        // Assert
        options.ShouldNotBeNull();
        options.Name.ShouldBe("test_app");
    }

    [Fact]
    public void GetOptions_Should_Set_Default_Values()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();

        // Act
        var options = configuration.GetOptions<AppOptions>("app");

        // Assert
        options.DisplayBanner.ShouldBeTrue();
        options.DisplayVersion.ShouldBeTrue();
    }

    [Fact]
    public void GetOptions_Should_Override_Default_Values()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                {"app:displayBanner", "false"},
                {"app:displayVersion", "false"}
            })
            .Build();

        // Act
        var options = configuration.GetOptions<AppOptions>("app");

        // Assert
        options.DisplayBanner.ShouldBeFalse();
        options.DisplayVersion.ShouldBeFalse();
    }

    // Test helper classes
    private class TestInitializer : IInitializer
    {
        public System.Threading.Tasks.Task InitializeAsync(System.Threading.CancellationToken cancellationToken)
        {
            return System.Threading.Tasks.Task.CompletedTask;
        }
    }

    private class AnotherTestInitializer : IInitializer
    {
        public System.Threading.Tasks.Task InitializeAsync(System.Threading.CancellationToken cancellationToken)
        {
            return System.Threading.Tasks.Task.CompletedTask;
        }
    }
}
