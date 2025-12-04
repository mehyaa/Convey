using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Convey.HTTP.Tests
{
    public class ExtensionsTests
    {
        [Fact]
        public void AddHttpClient_Should_Register_Services()
        {
            // Arrange
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder().Build();
            var builder = ConveyBuilder.Create(services, configuration);

            // Act
            builder.AddHttpClient();

            // Assert
            services.ShouldContain(s => s.ServiceType == typeof(HttpClientOptions));
        }

        [Fact]
        public void AddHttpClient_Should_Register_CorrelationIdFactories()
        {
            // Arrange
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder().Build();
            var builder = ConveyBuilder.Create(services, configuration);

            // Act
            builder.AddHttpClient();

            // Assert
            services.ShouldContain(s => s.ServiceType == typeof(ICorrelationIdFactory));
            services.ShouldContain(s => s.ServiceType == typeof(ICorrelationContextFactory));
        }
    }
}
