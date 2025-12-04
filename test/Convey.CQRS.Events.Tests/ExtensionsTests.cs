using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Convey.CQRS.Events;
using Convey.Types;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Convey.CQRS.Events.Tests;

public class ExtensionsTests
{
    [Fact]
    public void AddEventHandlers_Should_Register_Handlers()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = ConveyBuilder.Create(services);

        // Act
        builder.AddEventHandlers();

        // Assert
        builder.Services.ShouldNotBeNull();
    }

    [Fact]
    public void AddInMemoryEventDispatcher_Should_Register_Dispatcher()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = ConveyBuilder.Create(services);

        // Act
        builder.AddInMemoryEventDispatcher();

        // Assert
        services.ShouldContain(s => s.ServiceType == typeof(IEventDispatcher));
    }

    [Fact]
    public void AddInMemoryEventDispatcher_Should_Register_As_Singleton()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = ConveyBuilder.Create(services);

        // Act
        builder.AddInMemoryEventDispatcher();

        // Assert
        var descriptor = services.First(s => s.ServiceType == typeof(IEventDispatcher));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }

    [Fact]
    public async Task EventDispatcher_Should_Dispatch_Events()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = ConveyBuilder.Create(services);
        builder.AddEventHandlers();
        builder.AddInMemoryEventDispatcher();
        services.AddTransient<IEventHandler<TestEvent>, TestEventHandler>();

        var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<IEventDispatcher>();
        var testEvent = new TestEvent { Value = "test" };

        // Act
        await dispatcher.PublishAsync(testEvent);

        // Assert
        testEvent.Handled.ShouldBeTrue();
    }

    [Fact]
    public async Task EventDispatcher_Should_Handle_Multiple_Handlers()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = ConveyBuilder.Create(services);
        builder.AddInMemoryEventDispatcher();
        services.AddTransient<IEventHandler<TestEvent>, TestEventHandler>();
        services.AddTransient<IEventHandler<TestEvent>, AnotherTestEventHandler>();

        var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<IEventDispatcher>();
        var testEvent = new TestEvent();

        // Act
        await dispatcher.PublishAsync(testEvent);

        // Assert
        testEvent.HandlerCount.ShouldBe(2);
    }

    [Fact]
    public async Task EventDispatcher_Should_Pass_CancellationToken()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = ConveyBuilder.Create(services);
        builder.AddInMemoryEventDispatcher();
        services.AddTransient<IEventHandler<TestEvent>, CancellationTokenCapturingHandler>();

        var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<IEventDispatcher>();
        var cts = new CancellationTokenSource();
        var testEvent = new TestEvent();

        // Act
        await dispatcher.PublishAsync(testEvent, cts.Token);

        // Assert
        testEvent.CapturedToken.ShouldBe(cts.Token);
    }

    [Fact]
    public void AddEventHandlers_Should_Exclude_Decorators()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = ConveyBuilder.Create(services);

        // Act
        builder.AddEventHandlers();

        // Assert
        services.ShouldNotContain(s => s.ImplementationType == typeof(DecoratedEventHandler));
    }

    // Test helper classes
    public class TestEvent : IEvent
    {
        public string Value { get; set; }
        public bool Handled { get; set; }
        public int HandlerCount { get; set; }
        public CancellationToken CapturedToken { get; set; }
    }

    public class TestEventHandler : IEventHandler<TestEvent>
    {
        public Task HandleAsync(TestEvent @event, CancellationToken cancellationToken = default)
        {
            @event.Handled = true;
            @event.HandlerCount++;
            return Task.CompletedTask;
        }
    }

    public class AnotherTestEventHandler : IEventHandler<TestEvent>
    {
        public Task HandleAsync(TestEvent @event, CancellationToken cancellationToken = default)
        {
            @event.HandlerCount++;
            return Task.CompletedTask;
        }
    }

    public class CancellationTokenCapturingHandler : IEventHandler<TestEvent>
    {
        public Task HandleAsync(TestEvent @event, CancellationToken cancellationToken = default)
        {
            @event.CapturedToken = cancellationToken;
            return Task.CompletedTask;
        }
    }

    [Decorator]
    public class DecoratedEventHandler : IEventHandler<TestEvent>
    {
        public Task HandleAsync(TestEvent @event, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
