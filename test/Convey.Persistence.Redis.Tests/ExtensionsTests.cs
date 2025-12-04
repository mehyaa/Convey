using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Convey.Persistence.Redis.Tests
{
    public class ExtensionsTests
    {
        [Fact]
        public void AddRedis_Should_Register_Services()
        {
            // Arrange
            var services = new ServiceCollection();
            var builder = ConveyBuilder.Create(services);
            var options = new RedisOptions { ConnectionString = "localhost" };

            // Act
            builder.AddRedis(options);

            // Assert
            services.ShouldContain(s => s.ServiceType == typeof(RedisOptions));
        }

        [Fact]
        public void AddRedis_Should_Use_Registry()
        {
            // Arrange
            var services = new ServiceCollection();
            var builder = ConveyBuilder.Create(services);
            var options = new RedisOptions { ConnectionString = "localhost" };

            // Act
            builder.AddRedis(options);
            var result = builder.TryRegister("persistence.redis");

            // Assert
            result.ShouldBeFalse(); // Already registered
        }
    }
}
