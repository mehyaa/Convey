using Polly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Convey.HTTP;

public class ConveyHttpClient : IHttpClient
{
    private const string JsonContentType = "application/json";

    private readonly HttpClient _client;
    private readonly HttpClientOptions _options;
    private readonly IHttpClientSerializer _serializer;

    public ConveyHttpClient(
        HttpClient client,
        HttpClientOptions options,
        IHttpClientSerializer serializer,
        ICorrelationContextFactory correlationContextFactory,
        ICorrelationIdFactory correlationIdFactory)
    {
        _client = client;
        _options = options;
        _serializer = serializer;

        if (!string.IsNullOrWhiteSpace(_options.CorrelationContextHeader))
        {
            var correlationContext = correlationContextFactory.Create();

            _client.DefaultRequestHeaders.TryAddWithoutValidation(
                _options.CorrelationContextHeader,
                correlationContext);
        }

        if (!string.IsNullOrWhiteSpace(_options.CorrelationIdHeader))
        {
            var correlationId = correlationIdFactory.Create();

            _client.DefaultRequestHeaders.TryAddWithoutValidation(
                _options.CorrelationIdHeader,
                correlationId);
        }
    }

    public virtual Task<HttpResponseMessage> GetAsync(
        string uri,
        CancellationToken cancellationToken = default)
        => SendAsync(uri, Method.Get, cancellationToken: cancellationToken);

    public virtual Task<T> GetAsync<T>(
        string uri,
        IHttpClientSerializer serializer = null,
        CancellationToken cancellationToken = default)
        => SendAsync<T>(uri, Method.Get, serializer: serializer, cancellationToken: cancellationToken);

    public Task<HttpResult<T>> GetResultAsync<T>(
        string uri,
        IHttpClientSerializer serializer = null,
        CancellationToken cancellationToken = default)
        => SendResultAsync<T>(uri, Method.Get, serializer: serializer, cancellationToken: cancellationToken);

    public virtual Task<HttpResponseMessage> HeadAsync(
        string uri,
        CancellationToken cancellationToken = default)
        => SendAsync(uri, Method.Head, cancellationToken: cancellationToken);

    public virtual async Task<bool?> HeadAsync(
        string uri,
        IHttpClientSerializer serializer = null,
        CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(uri, Method.Head, cancellationToken: cancellationToken);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            return true;
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        return null;
    }

    public async Task<HttpResult<bool?>> HeadResultAsync(
        string uri,
        IHttpClientSerializer serializer = null,
        CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(uri, Method.Head, cancellationToken: cancellationToken);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            return new HttpResult<bool?>(true, response);
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new HttpResult<bool?>(false, response);
        }

        return new HttpResult<bool?>(null, response);
    }

    public virtual Task<HttpResponseMessage> PostAsync(
        string uri,
        object data = null,
        IHttpClientSerializer serializer = null,
        CancellationToken cancellationToken = default)
        => SendAsync(uri, Method.Post, GetJsonPayload(data, serializer), cancellationToken);

    public Task<HttpResponseMessage> PostAsync(
        string uri,
        HttpContent content,
        CancellationToken cancellationToken = default)
        => SendAsync(uri, Method.Post, content, cancellationToken);

    public virtual Task<T> PostAsync<T>(
        string uri,
        object data = null,
        IHttpClientSerializer serializer = null,
        CancellationToken cancellationToken = default)
        => SendAsync<T>(uri, Method.Post, GetJsonPayload(data, serializer), cancellationToken: cancellationToken);

    public Task<T> PostAsync<T>(
        string uri,
        HttpContent content,
        IHttpClientSerializer serializer = null,
        CancellationToken cancellationToken = default)
        => SendAsync<T>(uri, Method.Post, content, serializer, cancellationToken);

    public Task<HttpResult<T>> PostResultAsync<T>(
        string uri,
        object data = null,
        IHttpClientSerializer serializer = null,
        CancellationToken cancellationToken = default)
        => SendResultAsync<T>(uri, Method.Post, GetJsonPayload(data, serializer), serializer, cancellationToken);

    public Task<HttpResult<T>> PostResultAsync<T>(
        string uri,
        HttpContent content,
        IHttpClientSerializer serializer = null,
        CancellationToken cancellationToken = default)
        => SendResultAsync<T>(uri, Method.Post, content, serializer, cancellationToken);

    public virtual Task<HttpResponseMessage> PutAsync(
        string uri,
        object data = null,
        IHttpClientSerializer serializer = null,
        CancellationToken cancellationToken = default)
        => SendAsync(uri, Method.Put, GetJsonPayload(data, serializer), cancellationToken);

    public Task<HttpResponseMessage> PutAsync(
        string uri,
        HttpContent content,
        CancellationToken cancellationToken = default)
        => SendAsync(uri, Method.Put, content, cancellationToken);

    public virtual Task<T> PutAsync<T>(
        string uri,
        object data = null,
        IHttpClientSerializer serializer = null,
        CancellationToken cancellationToken = default)
        => SendAsync<T>(uri, Method.Put, GetJsonPayload(data, serializer), serializer, cancellationToken);

    public Task<T> PutAsync<T>(
        string uri,
        HttpContent content,
        IHttpClientSerializer serializer = null,
        CancellationToken cancellationToken = default)
        => SendAsync<T>(uri, Method.Put, content, serializer, cancellationToken);

    public Task<HttpResult<T>> PutResultAsync<T>(
        string uri,
        object data = null,
        IHttpClientSerializer serializer = null,
        CancellationToken cancellationToken = default)
        => SendResultAsync<T>(uri, Method.Put, GetJsonPayload(data, serializer), serializer, cancellationToken);

    public Task<HttpResult<T>> PutResultAsync<T>(
        string uri,
        HttpContent content,
        IHttpClientSerializer serializer = null,
        CancellationToken cancellationToken = default)
        => SendResultAsync<T>(uri, Method.Put, content, serializer, cancellationToken);

    public Task<HttpResponseMessage> PatchAsync(
        string uri,
        object data = null,
        IHttpClientSerializer serializer = null,
        CancellationToken cancellationToken = default)
        => SendAsync(uri, Method.Patch, GetJsonPayload(data, serializer), cancellationToken);

    public Task<HttpResponseMessage> PatchAsync(
        string uri,
        HttpContent content,
        CancellationToken cancellationToken = default)
        => SendAsync(uri, Method.Patch, content, cancellationToken);

    public Task<T> PatchAsync<T>(
        string uri,
        object data = null,
        IHttpClientSerializer serializer = null,
        CancellationToken cancellationToken = default)
        => SendAsync<T>(uri, Method.Patch, GetJsonPayload(data, serializer), cancellationToken: cancellationToken);

    public Task<T> PatchAsync<T>(
        string uri,
        HttpContent content,
        IHttpClientSerializer serializer = null,
        CancellationToken cancellationToken = default)
        => SendAsync<T>(uri, Method.Patch, content, serializer, cancellationToken);

    public Task<HttpResult<T>> PatchResultAsync<T>(
        string uri,
        object data = null,
        IHttpClientSerializer serializer = null,
        CancellationToken cancellationToken = default)
        => SendResultAsync<T>(uri, Method.Patch, GetJsonPayload(data, serializer), cancellationToken: cancellationToken);

    public Task<HttpResult<T>> PatchResultAsync<T>(
        string uri,
        HttpContent content,
        IHttpClientSerializer serializer = null,
        CancellationToken cancellationToken = default)
        => SendResultAsync<T>(uri, Method.Patch, content, serializer, cancellationToken);

    public virtual Task<HttpResponseMessage> DeleteAsync(string uri, CancellationToken cancellationToken = default)
        => SendAsync(uri, Method.Delete, cancellationToken: cancellationToken);

    public Task<T> DeleteAsync<T>(
        string uri,
        IHttpClientSerializer serializer = null,
        CancellationToken cancellationToken = default)
        => SendAsync<T>(uri, Method.Delete, serializer: serializer, cancellationToken: cancellationToken);

    public Task<HttpResult<T>> DeleteResultAsync<T>(
        string uri,
        IHttpClientSerializer serializer = null,
        CancellationToken cancellationToken = default)
        => SendResultAsync<T>(uri, Method.Delete, serializer: serializer, cancellationToken: cancellationToken);

    public Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
        => Policy.Handle<Exception>()
            .WaitAndRetryAsync(_options.Retries, r => TimeSpan.FromSeconds(Math.Pow(2, r)))
            .ExecuteAsync(() => _client.SendAsync(request, cancellationToken));

    public Task<T> SendAsync<T>(
        HttpRequestMessage request, IHttpClientSerializer serializer = null,
        CancellationToken cancellationToken = default)
        => Policy.Handle<Exception>()
            .WaitAndRetryAsync(_options.Retries, r => TimeSpan.FromSeconds(Math.Pow(2, r)))
            .ExecuteAsync(async () =>
            {
                var response = await _client.SendAsync(request, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    return default;
                }

                var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

                return await DeserializeJsonFromStreamAsync<T>(stream, serializer);
            });

    public Task<HttpResult<T>> SendResultAsync<T>(
        HttpRequestMessage request,
        IHttpClientSerializer serializer = null,
        CancellationToken cancellationToken = default)
        => Policy.Handle<Exception>()
            .WaitAndRetryAsync(_options.Retries, r => TimeSpan.FromSeconds(Math.Pow(2, r)))
            .ExecuteAsync(async () =>
            {
                var response = await _client.SendAsync(request, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    return new HttpResult<T>(default, response);
                }

                var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var result = await DeserializeJsonFromStreamAsync<T>(stream, serializer);

                return new HttpResult<T>(result, response);
            });

    public void SetHeaders(IDictionary<string, string> headers)
    {
        if (headers is null)
        {
            return;
        }

        foreach (var (key, value) in headers)
        {
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            _client.DefaultRequestHeaders.TryAddWithoutValidation(key, value);
        }
    }

    public void SetHeaders(Action<HttpRequestHeaders> headers) => headers?.Invoke(_client.DefaultRequestHeaders);

    protected virtual async Task<T> SendAsync<T>(
        string uri,
        Method method,
        HttpContent content = null,
        IHttpClientSerializer serializer = null,
        CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(uri, method, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return default;
        }

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        return await DeserializeJsonFromStreamAsync<T>(stream, serializer, cancellationToken);
    }

    protected virtual async Task<HttpResult<T>> SendResultAsync<T>(
        string uri,
        Method method,
        HttpContent content = null,
        IHttpClientSerializer serializer = null,
        CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(uri, method, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new HttpResult<T>(default, response);
        }

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var result = await DeserializeJsonFromStreamAsync<T>(stream, serializer, cancellationToken);

        return new HttpResult<T>(result, response);
    }

    protected virtual Task<HttpResponseMessage> SendAsync(
        string uri,
        Method method,
        HttpContent content = null,
        CancellationToken cancellationToken = default)
        => Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(_options.Retries, r => TimeSpan.FromSeconds(Math.Pow(2, r)))
            .ExecuteAsync(() =>
            {
                var requestUri = uri.StartsWith("http") ? uri : $"http://{uri}";

                return GetResponseAsync(requestUri, method, content, cancellationToken);
            });

    protected virtual Task<HttpResponseMessage> GetResponseAsync(
        string uri,
        Method method,
        HttpContent content = null,
        CancellationToken cancellationToken = default)
        => method switch
        {
            Method.Get => _client.GetAsync(uri, cancellationToken),
            Method.Head => _client.SendAsync(new HttpRequestMessage { RequestUri = new Uri(uri, UriKind.RelativeOrAbsolute), Method = HttpMethod.Head }, cancellationToken),
            Method.Post => _client.PostAsync(uri, content, cancellationToken),
            Method.Put => _client.PutAsync(uri, content, cancellationToken),
            Method.Patch => _client.PatchAsync(uri, content, cancellationToken),
            Method.Delete => _client.DeleteAsync(uri, cancellationToken),
            _ => throw new InvalidOperationException($"Unsupported HTTP method: {method}")
        };

    protected StringContent GetJsonPayload(object data, IHttpClientSerializer serializer = null)
    {
        if (data is null)
        {
            return null;
        }

        serializer ??= _serializer;

        var content = new StringContent(serializer.Serialize(data), Encoding.UTF8, JsonContentType);

        if (_options.RemoveCharsetFromContentType && content.Headers.ContentType is not null)
        {
            content.Headers.ContentType.CharSet = null;
        }

        return content;
    }

    protected async Task<T> DeserializeJsonFromStreamAsync<T>(
        Stream stream,
        IHttpClientSerializer serializer = null,
        CancellationToken cancellationToken = default)
    {
        if (stream is null || stream.CanRead is false)
        {
            return default;
        }

        serializer ??= _serializer;

        return await serializer.DeserializeAsync<T>(stream, cancellationToken);
    }

    protected enum Method
    {
        Get,
        Head,
        Post,
        Put,
        Patch,
        Delete
    }
}