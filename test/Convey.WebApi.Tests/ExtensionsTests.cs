using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Convey.WebApi.Tests
{
    public class ExtensionsTests
    {
        [Fact]
        public void AddWebApi_Should_Register_Services()
        {
            // Arrange
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder().Build();
            var builder = ConveyBuilder.Create(services, configuration);

            // Act
            builder.AddWebApi();

            // Assert
            services.ShouldContain(s => s.ServiceType == typeof(WebApiOptions));
        }
    }
}
