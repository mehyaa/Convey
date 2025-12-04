using System;
using Convey.Types;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Convey.Tests;

public class ServiceIdTests
{
    [Fact]
    public void Id_Should_Include_Service_Name()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new System.Collections.Generic.KeyValuePair<string, string>("app:service", "test-service")
            })
            .Build();

        services.AddConvey(configuration: configuration);
        var provider = services.BuildServiceProvider();
        var serviceId = provider.GetRequiredService<IServiceId>();

        // Act
        var id = serviceId.Id;

        // Assert
        id.ShouldNotBeNullOrEmpty();
        id.ShouldContain("test-service");
    }

    [Fact]
    public void Id_Should_Be_Consistent_On_Multiple_Calls()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new System.Collections.Generic.KeyValuePair<string, string>("app:service", "test-service")
            })
            .Build();

        services.AddConvey(configuration: configuration);
        var provider = services.BuildServiceProvider();
        var serviceId = provider.GetRequiredService<IServiceId>();

        // Act
        var id1 = serviceId.Id;
        var id2 = serviceId.Id;

        // Assert
        id1.ShouldBe(id2);
    }

    [Fact]
    public void Id_Should_Contain_Colon_Separator()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new System.Collections.Generic.KeyValuePair<string, string>("app:service", "test-service")
            })
            .Build();

        services.AddConvey(configuration: configuration);
        var provider = services.BuildServiceProvider();
        var serviceId = provider.GetRequiredService<IServiceId>();

        // Act
        var id = serviceId.Id;

        // Assert
        id.ShouldContain(":");
    }

    [Fact]
    public void Id_Should_Handle_Empty_Service_Name()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddConvey(configuration: configuration);
        var provider = services.BuildServiceProvider();
        var serviceId = provider.GetRequiredService<IServiceId>();

        // Act
        var id = serviceId.Id;

        // Assert
        id.ShouldNotBeNull();
    }

    [Fact]
    public void Different_Instances_Should_Have_Different_Ids()
    {
        // Arrange
        var services1 = new ServiceCollection();
        var configuration1 = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new System.Collections.Generic.KeyValuePair<string, string>("app:service", "service1")
            })
            .Build();
        services1.AddConvey(configuration: configuration1);
        var provider1 = services1.BuildServiceProvider();
        var serviceId1 = provider1.GetRequiredService<IServiceId>();

        var services2 = new ServiceCollection();
        var configuration2 = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new System.Collections.Generic.KeyValuePair<string, string>("app:service", "service2")
            })
            .Build();
        services2.AddConvey(configuration: configuration2);
        var provider2 = services2.BuildServiceProvider();
        var serviceId2 = provider2.GetRequiredService<IServiceId>();

        // Act
        var id1 = serviceId1.Id;
        var id2 = serviceId2.Id;

        // Assert
        id1.ShouldNotBe(id2);
    }

    [Fact]
    public void Id_Should_Use_Docker_Format_When_In_Container()
    {
        // Arrange
        var originalEnv = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER");
        try
        {
            Environment.SetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER", "true");

            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new[]
                {
                    new System.Collections.Generic.KeyValuePair<string, string>("app:service", "docker-service")
                })
                .Build();

            services.AddConvey(configuration: configuration);
            var provider = services.BuildServiceProvider();
            var serviceId = provider.GetRequiredService<IServiceId>();

            // Act
            var id = serviceId.Id;

            // Assert
            id.ShouldStartWith("docker-service:");
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER", originalEnv);
        }
    }
}
