using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Convey.HTTP;

public interface IHttpClient
{
    Task<HttpResponseMessage> GetAsync(string uri, CancellationToken cancellationToken = default);
    Task<T> GetAsync<T>(string uri, IHttpClientSerializer serializer = null, CancellationToken cancellationToken = default);
    Task<HttpResult<T>> GetResultAsync<T>(string uri, IHttpClientSerializer serializer = null, CancellationToken cancellationToken = default);
    Task<HttpResponseMessage> HeadAsync(string uri, CancellationToken cancellationToken = default);
    Task<bool?> HeadAsync(string uri, IHttpClientSerializer serializer = null, CancellationToken cancellationToken = default);
    Task<HttpResult<bool?>> HeadResultAsync(string uri, IHttpClientSerializer serializer = null, CancellationToken cancellationToken = default);
    Task<HttpResponseMessage> PostAsync(string uri, object data = null, IHttpClientSerializer serializer = null, CancellationToken cancellationToken = default);
    Task<HttpResponseMessage> PostAsync(string uri, HttpContent content, CancellationToken cancellationToken = default);
    Task<T> PostAsync<T>(string uri, object data = null, IHttpClientSerializer serializer = null, CancellationToken cancellationToken = default);
    Task<T> PostAsync<T>(string uri, HttpContent content, IHttpClientSerializer serializer = null, CancellationToken cancellationToken = default);
    Task<HttpResult<T>> PostResultAsync<T>(string uri, object data = null, IHttpClientSerializer serializer = null, CancellationToken cancellationToken = default);
    Task<HttpResult<T>> PostResultAsync<T>(string uri, HttpContent content, IHttpClientSerializer serializer = null, CancellationToken cancellationToken = default);
    Task<HttpResponseMessage> PutAsync(string uri, object data = null, IHttpClientSerializer serializer = null, CancellationToken cancellationToken = default);
    Task<HttpResponseMessage> PutAsync(string uri, HttpContent content, CancellationToken cancellationToken = default);
    Task<T> PutAsync<T>(string uri, object data = null, IHttpClientSerializer serializer = null, CancellationToken cancellationToken = default);
    Task<T> PutAsync<T>(string uri, HttpContent content, IHttpClientSerializer serializer = null, CancellationToken cancellationToken = default);
    Task<HttpResult<T>> PutResultAsync<T>(string uri, object data = null, IHttpClientSerializer serializer = null, CancellationToken cancellationToken = default);
    Task<HttpResult<T>> PutResultAsync<T>(string uri, HttpContent content, IHttpClientSerializer serializer = null, CancellationToken cancellationToken = default);
    Task<HttpResponseMessage> PatchAsync(string uri, object data = null, IHttpClientSerializer serializer = null, CancellationToken cancellationToken = default);
    Task<HttpResponseMessage> PatchAsync(string uri, HttpContent content, CancellationToken cancellationToken = default);
    Task<T> PatchAsync<T>(string uri, object data = null, IHttpClientSerializer serializer = null, CancellationToken cancellationToken = default);
    Task<T> PatchAsync<T>(string uri, HttpContent content, IHttpClientSerializer serializer = null, CancellationToken cancellationToken = default);
    Task<HttpResult<T>> PatchResultAsync<T>(string uri, object data = null, IHttpClientSerializer serializer = null, CancellationToken cancellationToken = default);
    Task<HttpResult<T>> PatchResultAsync<T>(string uri, HttpContent content, IHttpClientSerializer serializer = null, CancellationToken cancellationToken = default);
    Task<HttpResponseMessage> DeleteAsync(string uri, CancellationToken cancellationToken = default);
    Task<T> DeleteAsync<T>(string uri, IHttpClientSerializer serializer = null, CancellationToken cancellationToken = default);
    Task<HttpResult<T>> DeleteResultAsync<T>(string uri, IHttpClientSerializer serializer = null, CancellationToken cancellationToken = default);
    Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default);
    Task<T> SendAsync<T>(HttpRequestMessage request, IHttpClientSerializer serializer = null, CancellationToken cancellationToken = default);
    Task<HttpResult<T>> SendResultAsync<T>(HttpRequestMessage request, IHttpClientSerializer serializer = null, CancellationToken cancellationToken = default);
    void SetHeaders(IDictionary<string, string> headers);
    void SetHeaders(Action<HttpRequestHeaders> headers);
}