using Convey.CQRS.Commands;
using Convey.CQRS.Events;
using Convey;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Convey.MessageBrokers.CQRS.Tests
{
    public class ExtensionsTests
    {
        [Fact]
        public void AddServiceBusCommandDispatcher_Should_Register_Dispatcher()
        {
            // Arrange
            var services = new ServiceCollection();
            var builder = ConveyBuilder.Create(services);

            // Act
            builder.AddServiceBusCommandDispatcher();

            // Assert
            services.ShouldContain(s => s.ServiceType == typeof(ICommandDispatcher));
        }

        [Fact]
        public void AddServiceBusEventDispatcher_Should_Register_Dispatcher()
        {
            // Arrange
            var services = new ServiceCollection();
            var builder = ConveyBuilder.Create(services);

            // Act
            builder.AddServiceBusEventDispatcher();

            // Assert
            services.ShouldContain(s => s.ServiceType == typeof(IEventDispatcher));
        }
    }
}
