using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Convey.Tests;

public class ConveyBuilderTests
{
    [Fact]
    public void Create_Should_Return_ConveyBuilder()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var builder = ConveyBuilder.Create(services);

        // Assert
        builder.ShouldNotBeNull();
        builder.Services.ShouldBe(services);
    }

    [Fact]
    public void Create_Should_Set_Configuration_When_Provided()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        // Act
        var builder = ConveyBuilder.Create(services, configuration);

        // Assert
        builder.Configuration.ShouldBe(configuration);
    }

    [Fact]
    public void Create_Should_Handle_Null_Configuration()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var builder = ConveyBuilder.Create(services, null);

        // Assert
        builder.Configuration.ShouldBeNull();
    }

    [Fact]
    public void TryRegister_Should_Return_True_When_Name_Is_New()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = ConveyBuilder.Create(services);

        // Act
        var result = builder.TryRegister("test");

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void TryRegister_Should_Return_False_When_Name_Is_Already_Registered()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = ConveyBuilder.Create(services);
        builder.TryRegister("test");

        // Act
        var result = builder.TryRegister("test");

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void TryRegister_Should_Handle_Multiple_Names()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = ConveyBuilder.Create(services);

        // Act
        var result1 = builder.TryRegister("name1");
        var result2 = builder.TryRegister("name2");
        var result3 = builder.TryRegister("name3");

        // Assert
        result1.ShouldBeTrue();
        result2.ShouldBeTrue();
        result3.ShouldBeTrue();
    }

    [Fact]
    public void TryRegister_Should_Be_Case_Sensitive()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = ConveyBuilder.Create(services);
        builder.TryRegister("test");

        // Act
        var result = builder.TryRegister("Test");

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void Build_Should_Execute_Build_Actions()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = ConveyBuilder.Create(services);
        var executed = false;

        builder.AddBuildAction(sp => executed = true);

        // Act
        builder.Build();

        // Assert
        executed.ShouldBeTrue();
    }

    [Fact]
    public void Build_Should_Execute_Multiple_Build_Actions()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = ConveyBuilder.Create(services);
        var counter = 0;

        builder.AddBuildAction(sp => counter++);
        builder.AddBuildAction(sp => counter++);
        builder.AddBuildAction(sp => counter++);

        // Act
        builder.Build();

        // Assert
        counter.ShouldBe(3);
    }

    [Fact]
    public void Build_Should_Execute_Actions_In_Order()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = ConveyBuilder.Create(services);
        var executionOrder = "";

        builder.AddBuildAction(sp => executionOrder += "A");
        builder.AddBuildAction(sp => executionOrder += "B");
        builder.AddBuildAction(sp => executionOrder += "C");

        // Act
        builder.Build();

        // Assert
        executionOrder.ShouldBe("ABC");
    }

    [Fact]
    public void Build_Should_Return_ServiceProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = ConveyBuilder.Create(services);

        // Act
        var serviceProvider = builder.Build();

        // Assert
        serviceProvider.ShouldNotBeNull();
        serviceProvider.ShouldBeOfType<ServiceProvider>();
    }

    [Fact]
    public void Build_Should_Provide_ServiceProvider_To_Actions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<string>("test_service");
        var builder = ConveyBuilder.Create(services);
        string capturedService = null;

        builder.AddBuildAction(sp => capturedService = sp.GetService<string>());

        // Act
        builder.Build();

        // Assert
        capturedService.ShouldBe("test_service");
    }

    [Fact]
    public void AddBuildAction_Should_Handle_Null_Action_Gracefully()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = ConveyBuilder.Create(services);

        // Act & Assert
        Should.NotThrow(() => builder.AddBuildAction(null));
    }

    [Fact]
    public void Build_Should_Throw_When_BuildAction_Is_Null()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = ConveyBuilder.Create(services);
        builder.AddBuildAction(null);

        // Act & Assert
        Should.Throw<NullReferenceException>(() => builder.Build());
    }

    [Fact]
    public void Services_Property_Should_Return_Original_ServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = ConveyBuilder.Create(services);

        services.AddSingleton<string>("test");

        // Act
        var builderServices = builder.Services;

        // Assert
        builderServices.ShouldBeSameAs(services);
        builderServices.Any(s => s.ServiceType == typeof(string)).ShouldBeTrue();
    }

    [Fact]
    public void Configuration_Property_Should_Return_Provided_Configuration()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new[] { new KeyValuePair<string, string>("key", "value") })
            .Build();
        var services = new ServiceCollection();

        // Act
        var builder = ConveyBuilder.Create(services, configuration);

        // Assert
        builder.Configuration.ShouldBe(configuration);
        builder.Configuration["key"].ShouldBe("value");
    }

    [Fact]
    public void TryRegister_Should_Handle_Empty_String()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = ConveyBuilder.Create(services);

        // Act
        var result = builder.TryRegister("");

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void TryRegister_Should_Distinguish_Empty_String_And_Whitespace()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = ConveyBuilder.Create(services);
        builder.TryRegister("");

        // Act
        var result = builder.TryRegister(" ");

        // Assert
        result.ShouldBeTrue();
    }
}
