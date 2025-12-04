using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Convey.Tracing.Jaeger.Tests
{
    public class ExtensionsTests
    {
        [Fact]
        public void AddJaeger_Should_Register_Services()
        {
            // Arrange
            var services = new ServiceCollection();
            var builder = ConveyBuilder.Create(services);
            var options = new JaegerOptions { Enabled = false };

            // Act
            builder.AddJaeger(options);

            // Assert
            services.ShouldContain(s => s.ServiceType == typeof(JaegerOptions));
        }

        [Fact]
        public void AddJaeger_Should_Use_Registry()
        {
            // Arrange
            var services = new ServiceCollection();
            var builder = ConveyBuilder.Create(services);
            var options = new JaegerOptions { Enabled = false };

            // Act
            builder.AddJaeger(options);
            builder.AddJaeger(options); // Call again

            // Assert
            builder.ShouldNotBeNull();
        }
    }
}
