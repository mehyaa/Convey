using Convey.Discovery.Consul.Models;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Convey.Discovery.Consul.Services;

internal sealed class ConsulService : IConsulService
{
    private static readonly StringContent EmptyRequest = GetPayload(new { });

    private const string Version = "v1";

    private readonly HttpClient _httpClient;

    public ConsulService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<HttpResponseMessage> RegisterServiceAsync(ServiceRegistration registration, CancellationToken cancellationToken)
        => _httpClient.PutAsync(GetEndpoint("agent/service/register"), GetPayload(registration), cancellationToken);

    public Task<HttpResponseMessage> DeregisterServiceAsync(string id, CancellationToken cancellationToken)
        => _httpClient.PutAsync(GetEndpoint($"agent/service/deregister/{id}"), EmptyRequest, cancellationToken);

    public Task<HttpResponseMessage> PassCheckAsync(string id, CancellationToken cancellationToken)
        => _httpClient.PutAsync(GetEndpoint($"agent/check/pass/{id}"), EmptyRequest, cancellationToken);

    public async Task<IDictionary<string, ServiceAgent>> GetServiceAgentsAsync(string service = null, CancellationToken cancellationToken = default)
    {
        var filter = string.IsNullOrWhiteSpace(service) ? string.Empty : $"?filter=Service==\"{service}\"";

        var response = await _httpClient.GetAsync(GetEndpoint($"agent/services{filter}"), cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new Dictionary<string, ServiceAgent>();
        }

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        return await JsonSerializer.DeserializeAsync<IDictionary<string, ServiceAgent>>(stream, cancellationToken: cancellationToken);
    }

    private static StringContent GetPayload(object request)
        => new(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

    private static string GetEndpoint(string endpoint)
        => $"{Version}/{endpoint}";
}