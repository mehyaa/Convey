using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Convey.Discovery.Consul.Models;
using Convey.Discovery.Consul.Services;
using Moq;
using Moq.Protected;
using Shouldly;
using Xunit;

namespace Convey.Discovery.Consul.Tests
{
    public class ConsulServiceTests
    {
        private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
        private readonly HttpClient _httpClient;
        private readonly ConsulService _consulService;

        public ConsulServiceTests()
        {
            _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_httpMessageHandlerMock.Object)
            {
                BaseAddress = new Uri("http://localhost:8500")
            };
            _consulService = new ConsulService(_httpClient);
        }

        [Fact]
        public async Task RegisterServiceAsync_Should_Send_Put_Request()
        {
            // Arrange
            var registration = new ServiceRegistration { Id = "test-id", Name = "test-service" };
            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Put &&
                        req.RequestUri.ToString().EndsWith("v1/agent/service/register")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

            // Act
            var response = await _consulService.RegisterServiceAsync(registration, CancellationToken.None);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        [Fact]
        public async Task DeregisterServiceAsync_Should_Send_Put_Request()
        {
            // Arrange
            var id = "test-id";
            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Put &&
                        req.RequestUri.ToString().EndsWith($"v1/agent/service/deregister/{id}")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

            // Act
            var response = await _consulService.DeregisterServiceAsync(id, CancellationToken.None);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        [Fact]
        public async Task PassCheckAsync_Should_Send_Put_Request()
        {
            // Arrange
            var id = "test-id";
            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Put &&
                        req.RequestUri.ToString().EndsWith($"v1/agent/check/pass/{id}")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

            // Act
            var response = await _consulService.PassCheckAsync(id, CancellationToken.None);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        [Fact]
        public async Task GetServiceAgentsAsync_Should_Return_Agents()
        {
            // Arrange
            var agents = new Dictionary<string, ServiceAgent>
            {
                { "service-1", new ServiceAgent { Id = "service-1", Service = "test-service" } }
            };
            var json = JsonSerializer.Serialize(agents);

            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Get &&
                        req.RequestUri.ToString().Contains("v1/agent/services")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json)
                });

            // Act
            var result = await _consulService.GetServiceAgentsAsync(cancellationToken: CancellationToken.None);

            // Assert
            result.ShouldNotBeNull();
            result.Count.ShouldBe(1);
            result.ContainsKey("service-1").ShouldBeTrue();
        }
    }
}
