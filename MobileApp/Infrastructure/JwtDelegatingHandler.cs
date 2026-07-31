namespace MobileApp.Infrastructure;

/// <summary>
/// Stub delegating handler — attaches JWT Bearer token to outbound API requests.
/// Real implementation added in agenda-buddy-tn7.
/// </summary>
public class JwtDelegatingHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        return base.SendAsync(request, cancellationToken);
    }
}
