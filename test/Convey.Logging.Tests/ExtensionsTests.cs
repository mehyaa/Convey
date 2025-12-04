using Convey.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Convey.Logging.Tests
{
    public class ExtensionsTests
    {
        [Fact]
        public void AddCorrelationContextLogging_Should_Register_Services()
        {
            // Arrange
            var services = new ServiceCollection();
            var builder = ConveyBuilder.Create(services);

            // Act
            builder.AddCorrelationContextLogging();

            // Assert
            services.ShouldContain(s => s.ServiceType == typeof(CorrelationContextLoggingMiddleware));
        }
    }
}
