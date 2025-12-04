using Convey.Discovery.Consul;
using Convey.HTTP;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Convey.LoadBalancing.Fabio.Tests
{
    public class ExtensionsTests
    {
        [Fact]
        public void AddFabio_Should_Register_Services()
        {
            // Arrange
            var services = new ServiceCollection();
            var builder = ConveyBuilder.Create(services);
            var fabioOptions = new FabioOptions { Enabled = false };
            var consulOptions = new ConsulOptions();
            var httpOptions = new HttpClientOptions();

            // Act
            builder.AddFabio(fabioOptions, consulOptions, httpOptions);

            // Assert
            services.ShouldContain(s => s.ServiceType == typeof(FabioOptions));
        }
    }
}
