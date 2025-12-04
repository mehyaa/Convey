using System.Threading.Tasks;

namespace Convey.Auth;

public interface IAccessTokenService
{
    Task<bool> IsTokenActiveAsync();
    Task<bool> IsTokenActiveAsync(string token);
    Task DeactivateTokenAsync();
    Task DeactivateTokenAsync(string token);
}