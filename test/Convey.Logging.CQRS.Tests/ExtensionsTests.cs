using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Convey.Logging.CQRS.Tests
{
    public class ExtensionsTests
    {
        [Fact]
        public void AddCommandHandlersLogging_Should_Register_Services()
        {
            // Arrange
            var services = new ServiceCollection();
            var builder = ConveyBuilder.Create(services);

            // Act
            builder.AddCommandHandlersLogging();

            // Assert
            builder.ShouldNotBeNull();
        }

        [Fact]
        public void AddEventHandlersLogging_Should_Register_Services()
        {
            // Arrange
            var services = new ServiceCollection();
            var builder = ConveyBuilder.Create(services);

            // Act
            builder.AddEventHandlersLogging();

            // Assert
            builder.ShouldNotBeNull();
        }
    }
}
