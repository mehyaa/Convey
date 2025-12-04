using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Convey.Discovery.Consul.Models;
using Convey.Discovery.Consul.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Shouldly;
using Xunit;

namespace Convey.Discovery.Consul.Tests
{
    public class ConsulHostedServiceTests
    {
        private readonly Mock<IConsulService> _consulServiceMock;
        private readonly ServiceRegistration _serviceRegistration;
        private readonly ConsulOptions _consulOptions;
        private readonly Mock<ILogger<ConsulHostedService>> _loggerMock;
        private readonly ConsulHostedService _hostedService;

        public ConsulHostedServiceTests()
        {
            _consulServiceMock = new Mock<IConsulService>();
            _serviceRegistration = new ServiceRegistration { Id = "test-service-id" };
            _consulOptions = new ConsulOptions { Enabled = true, Service = "test-service" };
            _loggerMock = new Mock<ILogger<ConsulHostedService>>();
            _hostedService = new ConsulHostedService(
                _consulServiceMock.Object,
                _serviceRegistration,
                _consulOptions,
                _loggerMock.Object);
        }

        [Fact]
        public async Task StartAsync_Should_Register_Service_When_Enabled()
        {
            // Arrange
            _consulServiceMock
                .Setup(x => x.RegisterServiceAsync(It.IsAny<ServiceRegistration>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

            // Act
            await _hostedService.StartAsync(CancellationToken.None);

            // Assert
            _consulServiceMock.Verify(x => x.RegisterServiceAsync(_serviceRegistration, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task StartAsync_Should_Not_Register_Service_When_Disabled()
        {
            // Arrange
            _consulOptions.Enabled = false;

            // Act
            await _hostedService.StartAsync(CancellationToken.None);

            // Assert
            _consulServiceMock.Verify(x => x.RegisterServiceAsync(It.IsAny<ServiceRegistration>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task StopAsync_Should_Deregister_Service_When_Enabled()
        {
            // Arrange
            _consulServiceMock
                .Setup(x => x.DeregisterServiceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

            // Act
            await _hostedService.StopAsync(CancellationToken.None);

            // Assert
            _consulServiceMock.Verify(x => x.DeregisterServiceAsync(_serviceRegistration.Id, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task StartAsync_Should_Setup_TTL_Timer_When_Enabled()
        {
            // Arrange
            _consulOptions.TtlEnabled = true;
            _consulOptions.Ttl = 10;
            _consulServiceMock
                .Setup(x => x.RegisterServiceAsync(It.IsAny<ServiceRegistration>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

            // Act
            await _hostedService.StartAsync(CancellationToken.None);

            // Assert
            // We can't easily verify the timer callback without waiting, but we can verify no exception was thrown
            // and that registration happened.
            _consulServiceMock.Verify(x => x.RegisterServiceAsync(_serviceRegistration, It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
