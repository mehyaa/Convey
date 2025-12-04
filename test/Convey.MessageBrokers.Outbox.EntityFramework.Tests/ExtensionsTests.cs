using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Convey.MessageBrokers.Outbox.EntityFramework.Tests
{
    public class ExtensionsTests
    {
        [Fact]
        public void AddEntityFrameworkOutbox_Should_Register_Services()
        {
            // Arrange
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder().Build();
            var builder = ConveyBuilder.Create(services, configuration);

            // Act
            builder.AddMessageOutbox(config => config.AddEntityFramework<TestContext>());

            // Assert
            builder.ShouldNotBeNull();
        }

        private sealed class TestContext : DbContext
        {
        }
    }
}
