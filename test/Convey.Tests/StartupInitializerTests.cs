using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Convey.Types;
using Shouldly;
using Xunit;

namespace Convey.Tests;

public class StartupInitializerTests
{
    [Fact]
    public async Task StartAsync_Should_Execute_All_Initializers()
    {
        // Arrange
        var initializer1 = new TestInitializer();
        var initializer2 = new TestInitializer();
        var initializers = new[] { initializer1, initializer2 };
        var startupInitializer = new StartupInitializer(initializers);

        // Act
        await startupInitializer.StartAsync(CancellationToken.None);

        // Assert
        initializer1.Initialized.ShouldBeTrue();
        initializer2.Initialized.ShouldBeTrue();
    }

    [Fact]
    public async Task StartAsync_Should_Execute_Initializers_In_Order()
    {
        // Arrange
        var executionOrder = new List<int>();
        var initializer1 = new OrderTrackingInitializer(executionOrder, 1);
        var initializer2 = new OrderTrackingInitializer(executionOrder, 2);
        var initializer3 = new OrderTrackingInitializer(executionOrder, 3);
        var initializers = new[] { initializer1, initializer2, initializer3 };
        var startupInitializer = new StartupInitializer(initializers);

        // Act
        await startupInitializer.StartAsync(CancellationToken.None);

        // Assert
        executionOrder.ShouldBe(new[] { 1, 2, 3 });
    }

    [Fact]
    public async Task StartAsync_Should_Handle_Empty_Initializers()
    {
        // Arrange
        var initializers = Enumerable.Empty<IInitializer>();
        var startupInitializer = new StartupInitializer(initializers);

        // Act & Assert
        await Should.NotThrowAsync(() => startupInitializer.StartAsync(CancellationToken.None));
    }

    [Fact]
    public async Task StartAsync_Should_Pass_CancellationToken_To_Initializers()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var initializer = new CancellationTokenCapturingInitializer();
        var initializers = new[] { initializer };
        var startupInitializer = new StartupInitializer(initializers);

        // Act
        await startupInitializer.StartAsync(cts.Token);

        // Assert
        initializer.ReceivedToken.ShouldBe(cts.Token);
    }

    [Fact]
    public async Task StopAsync_Should_Complete_Successfully()
    {
        // Arrange
        var initializers = Enumerable.Empty<IInitializer>();
        var startupInitializer = new StartupInitializer(initializers);

        // Act & Assert
        await Should.NotThrowAsync(() => startupInitializer.StopAsync(CancellationToken.None));
    }

    [Fact]
    public async Task StopAsync_Should_Return_Completed_Task()
    {
        // Arrange
        var initializers = Enumerable.Empty<IInitializer>();
        var startupInitializer = new StartupInitializer(initializers);

        // Act
        var task = startupInitializer.StopAsync(CancellationToken.None);

        // Assert
        task.IsCompleted.ShouldBeTrue();
        await task;
    }

    [Fact]
    public async Task StartAsync_Should_Handle_Single_Initializer()
    {
        // Arrange
        var initializer = new TestInitializer();
        var initializers = new[] { initializer };
        var startupInitializer = new StartupInitializer(initializers);

        // Act
        await startupInitializer.StartAsync(CancellationToken.None);

        // Assert
        initializer.Initialized.ShouldBeTrue();
    }

    // Test helper classes
    private class TestInitializer : IInitializer
    {
        public bool Initialized { get; private set; }

        public Task InitializeAsync(CancellationToken cancellationToken)
        {
            Initialized = true;
            return Task.CompletedTask;
        }
    }

    private class OrderTrackingInitializer : IInitializer
    {
        private readonly List<int> _executionOrder;
        private readonly int _id;

        public OrderTrackingInitializer(List<int> executionOrder, int id)
        {
            _executionOrder = executionOrder;
            _id = id;
        }

        public Task InitializeAsync(CancellationToken cancellationToken)
        {
            _executionOrder.Add(_id);
            return Task.CompletedTask;
        }
    }

    private class CancellationTokenCapturingInitializer : IInitializer
    {
        public CancellationToken ReceivedToken { get; private set; }

        public Task InitializeAsync(CancellationToken cancellationToken)
        {
            ReceivedToken = cancellationToken;
            return Task.CompletedTask;
        }
    }
}
