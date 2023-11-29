using System.Collections.Generic;

namespace Convey.Discovery.Consul.Models;

public class Proxy
{
    public IList<Upstream> Upstreams { get; set; }
}