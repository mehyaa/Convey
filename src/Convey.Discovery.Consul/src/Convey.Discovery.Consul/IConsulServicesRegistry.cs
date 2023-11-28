using Convey.Discovery.Consul.Models;
using System.Threading.Tasks;

namespace Convey.Discovery.Consul;

public interface IConsulServicesRegistry
{
    Task<ServiceAgent> GetAsync(string name);
}