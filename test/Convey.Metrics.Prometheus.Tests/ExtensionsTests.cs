using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Convey.Metrics.Prometheus.Tests
{
    public class ExtensionsTests
    {
        [Fact]
        public void AddPrometheus_Should_Register_Services()
        {
            // Arrange
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder().Build();
            var builder = ConveyBuilder.Create(services, configuration);

            // Act
            builder.AddPrometheus();

            // Assert
            services.ShouldContain(s => s.ServiceType == typeof(PrometheusOptions));
        }

        [Fact]
        public void AddPrometheus_When_Disabled_Should_Not_Register_Services()
        {
            // Arrange
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder().Build();
            var builder = ConveyBuilder.Create(services, configuration);

            // Act
            builder.AddPrometheus();
            var options = services.BuildServiceProvider().GetRequiredService<PrometheusOptions>();

            // Assert
            options.ShouldNotBeNull();
        }
    }
}
