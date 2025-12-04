using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Convey.Security.Tests
{
    public class ExtensionsTests
    {
        [Fact]
        public void AddSecurity_Should_Register_Services()
        {
            // Arrange
            var services = new ServiceCollection();
            var builder = ConveyBuilder.Create(services);

            // Act
            builder.AddSecurity();

            // Assert
            services.ShouldContain(s => s.ServiceType == typeof(IEncryptor));
            services.ShouldContain(s => s.ServiceType == typeof(IHasher));
            services.ShouldContain(s => s.ServiceType == typeof(ISigner));
        }
    }
}
