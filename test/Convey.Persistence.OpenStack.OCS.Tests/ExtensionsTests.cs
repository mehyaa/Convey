using Convey.Persistence.OpenStack.OCS;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Convey.Persistence.OpenStack.OCS.Tests
{
    public class ExtensionsTests
    {
        [Fact]
        public void AddOpenStackOCS_Should_Register_Services()
        {
            // Arrange
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder().Build();
            var builder = ConveyBuilder.Create(services, configuration);

            // Act
            builder.AddOcsClient();

            // Assert
            services.ShouldContain(s => s.ServiceType == typeof(OcsOptions));
        }
    }
}
