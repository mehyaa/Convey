using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Convey.CQRS.Queries;
using Convey.Types;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Convey.CQRS.Queries.Tests;

public class ExtensionsTests
{
    [Fact]
    public void AddQueryHandlers_Should_Register_Handlers()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = ConveyBuilder.Create(services);

        // Act
        builder.AddQueryHandlers();

        // Assert
        builder.Services.ShouldNotBeNull();
    }

    [Fact]
    public void AddInMemoryQueryDispatcher_Should_Register_Dispatcher()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = ConveyBuilder.Create(services);

        // Act
        builder.AddInMemoryQueryDispatcher();

        // Assert
        services.ShouldContain(s => s.ServiceType == typeof(IQueryDispatcher));
    }

    [Fact]
    public void AddInMemoryQueryDispatcher_Should_Register_As_Singleton()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = ConveyBuilder.Create(services);

        // Act
        builder.AddInMemoryQueryDispatcher();

        // Assert
        var descriptor = services.First(s => s.ServiceType == typeof(IQueryDispatcher));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }

    [Fact]
    public async Task QueryDispatcher_Should_Dispatch_Queries()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = ConveyBuilder.Create(services);
        builder.AddQueryHandlers();
        builder.AddInMemoryQueryDispatcher();
        services.AddTransient<IQueryHandler<TestQuery, TestResult>, TestQueryHandler>();

        var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<IQueryDispatcher>();
        var query = new TestQuery { Value = "test" };

        // Act
        var result = await dispatcher.QueryAsync(query);

        // Assert
        result.ShouldNotBeNull();
        result.Value.ShouldBe("test_result");
    }

    [Fact]
    public async Task QueryDispatcher_Should_Pass_CancellationToken()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = ConveyBuilder.Create(services);
        builder.AddInMemoryQueryDispatcher();
        services.AddTransient<IQueryHandler<TestQuery, TestResult>, CancellationTokenCapturingHandler>();

        var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<IQueryDispatcher>();
        var cts = new CancellationTokenSource();
        var query = new TestQuery();

        // Act
        var result = await dispatcher.QueryAsync(query, cts.Token);

        // Assert
        result.CapturedToken.ShouldBe(cts.Token);
    }

    [Fact]
    public void AddQueryHandlers_Should_Exclude_Decorators()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = ConveyBuilder.Create(services);

        // Act
        builder.AddQueryHandlers();

        // Assert
        services.ShouldNotContain(s => s.ImplementationType == typeof(DecoratedQueryHandler));
    }

    // Test helper classes
    public class TestQuery : IQuery<TestResult>
    {
        public string Value { get; set; }
    }

    public class TestResult
    {
        public string Value { get; set; }
        public CancellationToken CapturedToken { get; set; }
    }

    public class TestQueryHandler : IQueryHandler<TestQuery, TestResult>
    {
        public Task<TestResult> HandleAsync(TestQuery query, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TestResult { Value = "test_result" });
        }
    }

    public class CancellationTokenCapturingHandler : IQueryHandler<TestQuery, TestResult>
    {
        public Task<TestResult> HandleAsync(TestQuery query, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TestResult { CapturedToken = cancellationToken });
        }
    }

    [Decorator]
    public class DecoratedQueryHandler : IQueryHandler<TestQuery, TestResult>
    {
        public Task<TestResult> HandleAsync(TestQuery query, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TestResult());
        }
    }
}
