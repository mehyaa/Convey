using Convey.Types;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System.Collections.Generic;
using Xunit;

namespace Convey.Metrics.AppMetrics.Tests
{
    public class ExtensionsTests
    {
        [Fact]
        public void AddMetrics_Should_Register_Services()
        {
            // Arrange
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    {"metrics:enabled", "false"}, // Disable to avoid complex initialization
                    {"app:service", "test"}
                })
                .Build();
            var builder = ConveyBuilder.Create(services, configuration);

            // Act
            builder.AddMetrics();

            // Assert
            services.ShouldContain(s => s.ServiceType == typeof(MetricsOptions));
        }

        [Fact]
        public void AddMetrics_With_Options_Should_Register_Services()
        {
            // Arrange
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    {"app:service", "test"}
                })
                .Build();
            var builder = ConveyBuilder.Create(services, configuration);
            var metricsOptions = new MetricsOptions { Enabled = false };
            var appOptions = new AppOptions { Service = "test" };

            // Act
            builder.AddMetrics(metricsOptions, appOptions);

            // Assert
            services.ShouldContain(s => s.ServiceType == typeof(MetricsOptions));
        }
    }
}
