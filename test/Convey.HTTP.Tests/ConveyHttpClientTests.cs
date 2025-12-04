using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Moq.Protected;
using Shouldly;
using Xunit;

namespace Convey.HTTP.Tests
{
    public class ConveyHttpClientTests
    {
        private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
        private readonly HttpClient _httpClient;
        private readonly HttpClientOptions _options;
        private readonly Mock<IHttpClientSerializer> _serializerMock;
        private readonly Mock<ICorrelationContextFactory> _correlationContextFactoryMock;
        private readonly Mock<ICorrelationIdFactory> _correlationIdFactoryMock;
        private readonly ConveyHttpClient _conveyHttpClient;

        public ConveyHttpClientTests()
        {
            _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_httpMessageHandlerMock.Object)
            {
                BaseAddress = new Uri("http://localhost")
            };
            _options = new HttpClientOptions
            {
                Retries = 0,
                CorrelationContextHeader = "Correlation-Context",
                CorrelationIdHeader = "Correlation-ID"
            };
            _serializerMock = new Mock<IHttpClientSerializer>();
            _correlationContextFactoryMock = new Mock<ICorrelationContextFactory>();
            _correlationIdFactoryMock = new Mock<ICorrelationIdFactory>();

            _correlationContextFactoryMock.Setup(x => x.Create()).Returns("test-context");
            _correlationIdFactoryMock.Setup(x => x.Create()).Returns("test-id");

            _conveyHttpClient = new ConveyHttpClient(
                _httpClient,
                _options,
                _serializerMock.Object,
                _correlationContextFactoryMock.Object,
                _correlationIdFactoryMock.Object);
        }

        [Fact]
        public async Task GetAsync_Should_Send_Get_Request()
        {
            // Arrange
            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

            // Act
            var response = await _conveyHttpClient.GetAsync("test");

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        [Fact]
        public async Task PostAsync_Should_Send_Post_Request_With_Content()
        {
            // Arrange
            var data = new { Value = "test" };
            var json = "{\"Value\":\"test\"}";
            _serializerMock.Setup(x => x.Serialize(It.IsAny<object>())).Returns(json);

            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Post &&
                        req.Content != null),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

            // Act
            var response = await _conveyHttpClient.PostAsync("test", data);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        [Fact]
        public async Task PutAsync_Should_Send_Put_Request_With_Content()
        {
            // Arrange
            var data = new { Value = "test" };
            var json = "{\"Value\":\"test\"}";
            _serializerMock.Setup(x => x.Serialize(It.IsAny<object>())).Returns(json);

            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Put &&
                        req.Content != null),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

            // Act
            var response = await _conveyHttpClient.PutAsync("test", data);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        [Fact]
        public async Task DeleteAsync_Should_Send_Delete_Request()
        {
            // Arrange
            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Delete),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

            // Act
            var response = await _conveyHttpClient.DeleteAsync("test");

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        [Fact]
        public async Task SendAsync_Should_Inject_Correlation_Headers()
        {
            // Arrange
            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

            // Act
            await _conveyHttpClient.GetAsync("test");

            // Assert
            // Headers are injected in constructor, so we verify they are present on the client's default headers
            _httpClient.DefaultRequestHeaders.Contains("Correlation-Context").ShouldBeTrue();
            _httpClient.DefaultRequestHeaders.GetValues("Correlation-Context").ShouldContain("test-context");
            _httpClient.DefaultRequestHeaders.Contains("Correlation-ID").ShouldBeTrue();
            _httpClient.DefaultRequestHeaders.GetValues("Correlation-ID").ShouldContain("test-id");
        }

        [Fact]
        public async Task GetAsync_Generic_Should_Deserialize_Response()
        {
            // Arrange
            var expectedResult = new TestResult { Value = "result" };
            var json = "{\"Value\":\"result\"}";

            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json)
                });

            _serializerMock
                .Setup(x => x.DeserializeAsync<TestResult>(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _conveyHttpClient.GetAsync<TestResult>("test");

            // Assert
            result.ShouldBe(expectedResult);
        }

        public class TestResult
        {
            public string Value { get; set; }
        }
    }
}
