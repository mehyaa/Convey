using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Convey.Auth.Distributed.Tests
{
    public class ExtensionsTests
    {
        [Fact]
        public void AddDistributedAccessTokenValidator_Should_Register_Service()
        {
            // Arrange
            var services = new ServiceCollection();
            var builder = ConveyBuilder.Create(services);

            // Act
            builder.AddDistributedAccessTokenValidator();

            // Assert
            services.ShouldContain(s => s.ServiceType == typeof(IAccessTokenService));
        }

        [Fact]
        public void AddDistributedAccessTokenValidator_Should_Register_As_Singleton()
        {
            // Arrange
            var services = new ServiceCollection();
            var builder = ConveyBuilder.Create(services);

            // Act
            builder.AddDistributedAccessTokenValidator();

            // Assert
            var descriptor = services.First(s => s.ServiceType == typeof(IAccessTokenService));
            descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
        }

        [Fact]
        public void AddDistributedAccessTokenValidator_Should_Replace_Existing_Service()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddSingleton<IAccessTokenService, DummyAccessTokenService>();
            var builder = ConveyBuilder.Create(services);

            // Act
            builder.AddDistributedAccessTokenValidator();

            // Assert
            var descriptors = services.Where(s => s.ServiceType == typeof(IAccessTokenService)).ToList();
            descriptors.Count.ShouldBeGreaterThanOrEqualTo(1);
        }

        // Helper class
        private class DummyAccessTokenService : IAccessTokenService
        {
            public System.Threading.Tasks.Task<bool> IsTokenActiveAsync(string token) => System.Threading.Tasks.Task.FromResult(true);
            public System.Threading.Tasks.Task<bool> IsTokenActiveAsync() => System.Threading.Tasks.Task.FromResult(true);
            public System.Threading.Tasks.Task DeactivateTokenAsync(string token) => System.Threading.Tasks.Task.CompletedTask;
            public System.Threading.Tasks.Task DeactivateTokenAsync() => System.Threading.Tasks.Task.CompletedTask;
        }
    }
}
