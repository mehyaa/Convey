using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Primitives;

namespace Convey.Auth.Distributed;

internal sealed class DistributedAccessTokenService : IAccessTokenService
{
    private readonly IDistributedCache _cache;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly TimeSpan _expires;

    public DistributedAccessTokenService(IDistributedCache cache, IHttpContextAccessor httpContextAccessor,
        JwtOptions jwtOptions)
    {
        _cache = cache;
        _httpContextAccessor = httpContextAccessor;
        _expires = jwtOptions.Expiry ?? TimeSpan.FromMinutes(jwtOptions.ExpiryMinutes);
    }

    public Task<bool> IsTokenActiveAsync()
        => IsTokenActiveAsync(GetCurrent());

    public async Task<bool> IsTokenActiveAsync(string token)
        => string.IsNullOrWhiteSpace(await _cache.GetStringAsync(GetKey(token)));

    public Task DeactivateTokenAsync()
        => DeactivateTokenAsync(GetCurrent());

    public Task DeactivateTokenAsync(string token)
        => _cache.SetStringAsync(GetKey(token),
            "revoked", new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _expires
            });

    private static string GetKey(string token) => $"blacklisted-tokens:{token}";

    private string GetCurrent()
    {
        var authorizationHeader = _httpContextAccessor.HttpContext.Request.Headers.Authorization;

        return authorizationHeader == StringValues.Empty
            ? string.Empty
            : authorizationHeader.Single().Split(' ')[^1];
    }
}