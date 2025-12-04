using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Convey.WebApi.Swagger.Tests
{
    public class ExtensionsTests
    {
        [Fact]
        public void AddWebApiSwaggerDocs_Should_Register_Services()
        {
            // Arrange
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder().AddJsonStream(GetJsonStream()).Build();
            var builder = ConveyBuilder.Create(services, configuration);

            // Act
            builder.AddWebApiSwaggerDocs();

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
