using System.Security.Claims;
using System.Text;
using System.Text.Json;
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

        var parts = token.Split('.');
        if (parts.Length < 2)
            return;

        var payload = parts[1];
        // Fix base64url padding
        payload = payload.Replace('-', '+').Replace('_', '/');
        switch (payload.Length % 4)
        {
            case 2: payload += "=="; break;
            case 3: payload += "="; break;
        }

        var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Email = root.TryGetProperty("sub", out var sub) ? sub.GetString() ?? "" : "";

        // Role claim can be under the full URI or just "role"
        const string roleClaimUri = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";
        if (root.TryGetProperty(roleClaimUri, out var roleClaim))
            Role = roleClaim.GetString() ?? "";
        else if (root.TryGetProperty("role", out var roleShort))
            Role = roleShort.GetString() ?? "";
        else
            Role = "";
    }
}
