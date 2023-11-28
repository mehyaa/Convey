using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Convey.Discovery.Consul.Models;

namespace Convey.Discovery.Consul;

public interface IConsulService
{
    Task<HttpResponseMessage> RegisterServiceAsync(ServiceRegistration registration, CancellationToken cancellationToken);
    Task<HttpResponseMessage> DeregisterServiceAsync(string id, CancellationToken cancellationToken);
    Task<HttpResponseMessage> PassCheckAsync(string id, CancellationToken cancellationToken);
    Task<IDictionary<string, ServiceAgent>> GetServiceAgentsAsync(string service = null, CancellationToken cancellationToken = default);
}