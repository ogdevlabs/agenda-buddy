using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using MobileApp.Infrastructure;

namespace MobileApp.Services;

public interface IUserSessionService
{
    string Email { get; }
    string Role { get; }
    bool IsProvider { get; }
    bool IsCustomer { get; }
    Task RefreshAsync();
}

public class UserSessionService : IUserSessionService
{
    private readonly ISecureStorageService _secureStorage;

    public string Email { get; private set; } = string.Empty;
    public string Role { get; private set; } = string.Empty;
    public bool IsProvider => string.Equals(Role, "provider", StringComparison.OrdinalIgnoreCase);
    public bool IsCustomer => string.Equals(Role, "customer", StringComparison.OrdinalIgnoreCase);

    public UserSessionService(ISecureStorageService secureStorage)
    {
        _secureStorage = secureStorage;
    }

    public async Task RefreshAsync()
    {
        var token = await _secureStorage.GetAsync(JwtDelegatingHandler.JwtKey);
        if (string.IsNullOrEmpty(token))
        {
            Email = string.Empty;
            Role = string.Empty;
            return;
        }

        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(token))
            return;

        var jwt = handler.ReadJwtToken(token);
        Email = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value ?? string.Empty;
        Role = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role || c.Type == "role")?.Value ?? string.Empty;
    }
}
