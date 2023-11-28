using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Convey.Discovery.Consul.Models;

public class ServiceCheck
{
    public string Id { get; set; }
    public string Name { get; set; }

    public string Http { get; set; }
    public string Interval { get; set; }
    public string Timeout { get; set; }

    public string Ttl { get; set; }

    [JsonPropertyName("failures_before_warning")]
    public int FailuresBeforeWarning { get; set; } = 1;

    [JsonPropertyName("failures_before_critical")]
    public int FailuresBeforeCritical { get; set; } = 3;

    [JsonPropertyName("deregister_critical_service_after")]
    public string DeregisterCriticalServiceAfter { get; set; } = "5s";

    public List<string> Args { get; set; }
}