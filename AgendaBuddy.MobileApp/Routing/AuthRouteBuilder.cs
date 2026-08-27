namespace AgendaBuddy.MobileApp.Routing;

/// <summary>
/// Route-building logic extracted from <see cref="Services.AuthService"/> (F-015-T06).
/// Unlike the other builders in this namespace, these two routes are already correct — extracted
/// anyway so F-015-T09/T10's refresh/logout wiring can add methods here using the same
/// Maui-free, DI-free, directly-testable pattern.
/// </summary>
public static class AuthRouteBuilder
{
    public static RouteSpec Login() => new(HttpMethod.Post, "api/v1/auth/login");

    public static RouteSpec Register() => new(HttpMethod.Post, "api/v1/auth/register");

    /// <summary>
    /// Added by F-015-T09: <see cref="Infrastructure.JwtDelegatingHandler"/>'s transparent
    /// refresh-on-401 path calls this on the "AgendaBuddyApiNoAuth" client, since the access
    /// token is what just failed.
    /// </summary>
    public static RouteSpec Refresh() => new(HttpMethod.Post, "api/v1/auth/refresh");

    /// <summary>
    /// Added by F-015-T10 (AC11): <see cref="Services.AuthService.LogoutAsync"/> calls this,
    /// carrying the stored refresh token, so Identity invalidates it server-side
    /// (<c>AgendaBuddy.Identity/Program.cs:196</c>) instead of leaving it valid for its full 24-hour lifetime.
    /// </summary>
    public static RouteSpec Logout() => new(HttpMethod.Post, "api/v1/auth/logout");
}
