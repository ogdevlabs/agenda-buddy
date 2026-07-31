using System.Net;
using System.Net.Http.Headers;

namespace MobileApp.Infrastructure;

public class JwtDelegatingHandler : DelegatingHandler
{
    public const string JwtKey = "jwt";

    private readonly ISecureStorageService _secureStorage;

    public JwtDelegatingHandler(ISecureStorageService secureStorage)
    {
        _secureStorage = secureStorage;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await _secureStorage.GetAsync(JwtKey);
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            _secureStorage.Remove(JwtKey);
            UnauthorizedAccess?.Invoke(this, EventArgs.Empty);
        }

        return response;
    }

    public static event EventHandler? UnauthorizedAccess;
}
