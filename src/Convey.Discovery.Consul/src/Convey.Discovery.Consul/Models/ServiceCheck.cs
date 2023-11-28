using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Convey.Discovery.Consul.Models;

public class ServiceCheck
{
    [JsonPropertyName("CheckID")]
    public string CheckId { get; set; }

    public string Name { get; set; }

    [JsonPropertyName("HTTP")]
    public string Http { get; set; }

    public string Interval { get; set; }
    public string Timeout { get; set; }

    [JsonPropertyName("TTL")]
    public string Ttl { get; set; }

    public int FailuresBeforeWarning { get; set; } = 1;
    public int FailuresBeforeCritical { get; set; } = 3;
    public string DeregisterCriticalServiceAfter { get; set; } = "5s";

    public List<string> Args { get; set; }
}