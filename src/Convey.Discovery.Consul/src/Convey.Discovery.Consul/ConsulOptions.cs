using System.Collections.Generic;

namespace Convey.Discovery.Consul;

public class ConsulOptions
{
    public bool Enabled { get; set; }
    public string Url { get; set; }
    public string Service { get; set; }
    public bool UseAddress { get; set; }
    public string Address { get; set; }
    public int Port { get; set; }
    public bool PingEnabled { get; set; }
    public string PingEndpoint { get; set; }
    public string PingInterval { get; set; }
    public string PingTimeout { get; set; }
    public bool TtlEnabled { get; set; }
    public int Ttl { get; set; }
    public int WarningAfterFailure { get; set; } = 1;
    public int CriticalAfterFailure { get; set; } = 3;
    public string RemoveAfterInterval { get; set; } = "5s";
    public List<string> Tags { get; set; }
    public IDictionary<string, string> Meta { get; set; }
    public bool EnableTagOverride { get; set; }
    public bool SkipLocalhostDockerDnsReplace { get; set; }
    public ConnectOptions Connect { get; set; }

    public class ConnectOptions
    {
        public bool Enabled { get; set; }
    }
}