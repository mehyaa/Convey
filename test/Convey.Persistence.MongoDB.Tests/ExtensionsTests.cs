using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Convey.Persistence.MongoDB.Tests
{
    public class ExtensionsTests
    {
        [Fact]
        public void AddMongo_Should_Register_Services()
        {
            // Arrange
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder().Build();
            var builder = ConveyBuilder.Create(services, configuration);

            // Act
            builder.AddMongo();

            // Assert
            services.ShouldContain(s => s.ServiceType == typeof(MongoDbOptions));
        }
    }
}
