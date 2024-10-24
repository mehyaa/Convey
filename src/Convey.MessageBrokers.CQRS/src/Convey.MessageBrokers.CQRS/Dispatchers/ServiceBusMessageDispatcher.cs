using System.Threading;
using System.Threading.Tasks;
using Convey.CQRS.Commands;
using Convey.CQRS.Events;

namespace Convey.MessageBrokers.CQRS.Dispatchers;

internal sealed class ServiceBusMessageDispatcher : ICommandDispatcher, IEventDispatcher
{
    private readonly IBusPublisher _busPublisher;
    private readonly ICorrelationContextAccessor _accessor;

    public ServiceBusMessageDispatcher(IBusPublisher busPublisher, ICorrelationContextAccessor accessor)
    {
        _busPublisher = busPublisher;
        _accessor = accessor;
    }

    public Task SendAsync<T>(T command, CancellationToken cancellationToken = default)
        where T : class, ICommand
        => _busPublisher.SendAsync(command, _accessor.CorrelationContext, cancellationToken: cancellationToken);

    public Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default)
        where T : class, IEvent
        => _busPublisher.PublishAsync(@event, _accessor.CorrelationContext, cancellationToken: cancellationToken);
}