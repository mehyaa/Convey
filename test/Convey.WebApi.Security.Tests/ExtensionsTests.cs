using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Convey.WebApi.Security.Tests
{
    public class ExtensionsTests
    {
        [Fact]
        public void AddCertificateAuthentication_Should_Register_Services()
        {
            // Arrange
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder().Build();
            var builder = ConveyBuilder.Create(services, configuration);

            // Act
            builder.AddCertificateAuthentication();

            // Assert
            services.ShouldContain(s => s.ServiceType == typeof(SecurityOptions));
        }
    }
}
