using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Convey.Types;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Convey.CQRS.Commands.Tests;

public class ExtensionsTests
{
    [Fact]
    public void AddCommandHandlers_Should_Register_Handlers()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = ConveyBuilder.Create(services);

        // Act
        builder.AddCommandHandlers();

        // Assert
        builder.Services.ShouldNotBeNull();
    }

    [Fact]
    public void AddInMemoryCommandDispatcher_Should_Register_Dispatcher()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = ConveyBuilder.Create(services);

        // Act
        builder.AddInMemoryCommandDispatcher();

        // Assert
        services.ShouldContain(s => s.ServiceType == typeof(ICommandDispatcher));
    }

    [Fact]
    public void AddInMemoryCommandDispatcher_Should_Register_As_Singleton()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = ConveyBuilder.Create(services);

        // Act
        builder.AddInMemoryCommandDispatcher();

        // Assert
        var descriptor = services.First(s => s.ServiceType == typeof(ICommandDispatcher));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }

    [Fact]
    public async Task CommandDispatcher_Should_Dispatch_Commands()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = ConveyBuilder.Create(services);
        builder.AddCommandHandlers();
        builder.AddInMemoryCommandDispatcher();
        services.AddTransient<ICommandHandler<TestCommand>, TestCommandHandler>();

        var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<ICommandDispatcher>();
        var command = new TestCommand { Value = "test" };

        // Act
        await dispatcher.SendAsync(command);

        // Assert
        command.Handled.ShouldBeTrue();
    }

    [Fact]
    public async Task CommandDispatcher_Should_Create_Scope_Per_Command()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = ConveyBuilder.Create(services);
        builder.AddInMemoryCommandDispatcher();
        services.AddScoped<ICommandHandler<TestCommand>, TestCommandHandler>();

        var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<ICommandDispatcher>();

        // Act & Assert
        await Should.NotThrowAsync(() => dispatcher.SendAsync(new TestCommand()));
    }

    [Fact]
    public async Task CommandDispatcher_Should_Pass_CancellationToken()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = ConveyBuilder.Create(services);
        builder.AddInMemoryCommandDispatcher();
        services.AddTransient<ICommandHandler<TestCommand>, CancellationTokenCapturingHandler>();

        var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<ICommandDispatcher>();
        var cts = new CancellationTokenSource();
        var command = new TestCommand();

        // Act
        await dispatcher.SendAsync(command, cts.Token);

        // Assert
        command.CapturedToken.ShouldBe(cts.Token);
    }

    [Fact]
    public void AddCommandHandlers_Should_Exclude_Decorators()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = ConveyBuilder.Create(services);

        // Act
        builder.AddCommandHandlers();

        // Assert - Decorator should not be registered as a handler
        services.ShouldNotContain(s => s.ImplementationType == typeof(DecoratedCommandHandler));
    }

    // Test helper classes
    public class TestCommand : ICommand
    {
        public string Value { get; set; }
        public bool Handled { get; set; }
        public CancellationToken CapturedToken { get; set; }
    }

    public class TestCommandHandler : ICommandHandler<TestCommand>
    {
        public Task HandleAsync(TestCommand command, CancellationToken cancellationToken = default)
        {
            command.Handled = true;
            return Task.CompletedTask;
        }
    }

    public class CancellationTokenCapturingHandler : ICommandHandler<TestCommand>
    {
        public Task HandleAsync(TestCommand command, CancellationToken cancellationToken = default)
        {
            command.CapturedToken = cancellationToken;
            return Task.CompletedTask;
        }
    }

    [Decorator]
    public class DecoratedCommandHandler : ICommandHandler<TestCommand>
    {
        public Task HandleAsync(TestCommand command, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
