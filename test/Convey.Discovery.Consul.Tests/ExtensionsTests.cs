using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using Shouldly;
using Xunit;

namespace Convey.Discovery.Consul.Tests
{
    public class ExtensionsTests
    {
        [Fact]
        public void AddConsul_Should_Register_Services()
        {
            // Arrange
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder().AddJsonStream(GetJsonStream()).Build();

            // Act
            services.AddConvey(configuration: configuration).AddConsul();

            // Assert
            services.ShouldContain(s => s.ServiceType == typeof(ConsulOptions));
        }

        [Fact]
        public void AddConsul_Should_Register_Services_With_Options_Builder()
        {
            // Arrange
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder().Build();
            var httpClientOptions = new Convey.HTTP.HttpClientOptions();

            // Act
            services.AddConvey(configuration: configuration).AddConsul(b => b.Enable(true).WithUrl("http://localhost:8500").WithAddress("localhost"), httpClientOptions);

            // Assert
            services.ShouldContain(s => s.ServiceType == typeof(ConsulOptions));
            var provider = services.BuildServiceProvider();
            var options = provider.GetService<ConsulOptions>();
            options.Enabled.ShouldBeTrue();
            options.Url.ShouldBe("http://localhost:8500");
        }

        [Fact]
        public void AddConsul_Should_Configure_HttpClient()
        {
            // Arrange
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder().AddJsonStream(GetJsonStream()).Build();

            // Act
            services.AddConvey(configuration: configuration).AddConsul();

            // Assert
            services.ShouldContain(s => s.ServiceType == typeof(IHttpClientFactory));
        }

        private static System.IO.Stream GetJsonStream()
        {
            var json = @"
            {
                ""consul"": {
                    ""enabled"": true,
                    ""address"": ""http://localhost:8500""
                }
            }";

            return new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        }
    }
}
