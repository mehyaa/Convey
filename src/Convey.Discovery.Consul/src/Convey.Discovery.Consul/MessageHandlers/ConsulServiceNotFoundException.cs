using System;

namespace Convey.Discovery.Consul.MessageHandlers;

internal sealed class ConsulServiceNotFoundException : Exception
{
    public string ServiceName { get; set; }

    public ConsulServiceNotFoundException(string serviceName) : this(string.Empty, serviceName)
    {
    }

    public ConsulServiceNotFoundException(string message, string serviceName) : base(message)
    {
        ServiceName = serviceName;
    }

    public ConsulServiceNotFoundException()
    {
    }

    public ConsulServiceNotFoundException(string message, Exception innerException) : base(message, innerException)
    {
    }
}