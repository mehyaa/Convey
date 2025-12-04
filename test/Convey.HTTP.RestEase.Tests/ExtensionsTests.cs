using Convey.HTTP.RestEase;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Convey.HTTP.RestEase.Tests
{
    public class ExtensionsTests
    {
        [Fact]
        public void AddRestEaseClient_Should_Register_Services()
        {
            // Arrange
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder().Build();
            var builder = ConveyBuilder.Create(services, configuration);

            // Act
            builder.AddServiceClient<ITestService>("test");

            // Assert
            services.ShouldContain(s => s.ServiceType == typeof(ITestService));
        }

        public interface ITestService
        {
        }
    }
}
