using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Convey.MessageBrokers.Outbox.Tests
{
    public class ExtensionsTests
    {
        [Fact]
        public void AddMessageOutbox_Should_Register_Services()
        {
            // Arrange
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder().Build();
            var builder = ConveyBuilder.Create(services, configuration);

            // Act
            builder.AddMessageOutbox();

            // Assert
            builder.ShouldNotBeNull();
        }
    }
}
