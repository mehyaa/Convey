using System.Threading;

namespace Convey.MessageBrokers;

public class CorrelationContextAccessor : ICorrelationContextAccessor
{
    private static readonly AsyncLocal<CorrelationContextHolder> _asyncStore = new();

    public object CorrelationContext
    {
        get => _asyncStore.Value?.Context;
        set
        {
            var holder = _asyncStore.Value;

            if (holder is not null)
            {
                holder.Context = null;
            }

            if (value is not null)
            {
                _asyncStore.Value = new CorrelationContextHolder { Context = value };
            }
        }
    }

    private class CorrelationContextHolder
    {
        public object Context;
    }
}