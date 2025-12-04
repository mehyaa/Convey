using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Convey.WebApi.CQRS.Tests
{
    public class ExtensionsTests
    {
        [Fact]
        public void AddInMemoryDispatcher_Should_Register_Services()
        {
            // Arrange
            var services = new ServiceCollection();
            var builder = ConveyBuilder.Create(services);

            // Act
            builder.AddInMemoryDispatcher();

            // Assert
            services.ShouldContain(s => s.ServiceType == typeof(IDispatcher));
        }
    }
}
