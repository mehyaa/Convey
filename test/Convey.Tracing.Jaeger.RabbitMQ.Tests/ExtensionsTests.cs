using Convey.MessageBrokers.RabbitMQ;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using Xunit;

namespace Convey.Tracing.Jaeger.RabbitMQ.Tests
{
    public class ExtensionsTests
    {
        [Fact]
        public void AddJaegerRabbitMqPlugin_Should_Register_Plugin()
        {
            // Arrange
            var registryMock = new Mock<IRabbitMqPluginsRegistry>();

            // Act
            var result = registryMock.Object.AddJaegerRabbitMqPlugin();

            // Assert
            result.ShouldNotBeNull();
            registryMock.Verify(r => r.Add<Plugins.JaegerPlugin>(), Times.Once);
        }
    }
}
