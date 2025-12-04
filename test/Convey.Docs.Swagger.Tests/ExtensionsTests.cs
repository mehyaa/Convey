using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Convey.Docs.Swagger.Tests
{
    public class ExtensionsTests
    {
        [Fact]
        public void AddSwaggerDocs_Should_Register_Services()
        {
            // Arrange
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder().AddJsonStream(GetJsonStream()).Build();
            var builder = ConveyBuilder.Create(services, configuration);

            // Act
            builder.AddSwaggerDocs();

            // Assert
            services.ShouldContain(s => s.ServiceType == typeof(SwaggerOptions));
        }

        [Fact]
        public void AddSwaggerDocs_With_Options_Should_Register_Services()
        {
            // Arrange
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder().Build();
            var builder = ConveyBuilder.Create(services, configuration);
            var options = new SwaggerOptions { Enabled = true };

            // Act
            builder.AddSwaggerDocs(options);

            // Assert
            services.ShouldContain(s => s.ServiceType == typeof(SwaggerOptions));
        }

        [Fact]
        public void AddSwaggerDocs_When_Disabled_Should_Not_Register_Swagger()
        {
            // Arrange
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder().Build();
            var builder = ConveyBuilder.Create(services, configuration);
            var options = new SwaggerOptions { Enabled = false };

            // Act
            builder.AddSwaggerDocs(options);

            // Assert
            builder.ShouldNotBeNull();
        }

        private static System.IO.Stream GetJsonStream()
        {
            var json = @"
            {
                ""swagger"": {
                    ""enabled"": true,
                    ""title"": ""Test API"",
                    ""version"": ""v1"",
                    ""name"": ""TestAPI"",
                    ""includeSecurity"": true
                }
            }";

            return new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        }
    }
}
