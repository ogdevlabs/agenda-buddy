namespace AgendaBuddy.MobileApp.Routing;

/// <summary>
/// Route-building logic extracted from <see cref="Services.AuthService"/>.
/// Unlike the other builders in this namespace, these two routes are already correct — extracted
/// anyway so refresh/logout wiring can add methods here using the same
/// Maui-free, DI-free, directly-testable pattern.
/// </summary>
public static class AuthRouteBuilder
{
    public static RouteSpec Login() => new(HttpMethod.Post, "api/v1/auth/login");

    public static RouteSpec Register() => new(HttpMethod.Post, "api/v1/auth/register");

    /// <summary>
    /// <see cref="Infrastructure.JwtDelegatingHandler"/>'s transparent
    /// refresh-on-401 path calls this on the "AgendaBuddyApiNoAuth" client, since the access
    /// token is what just failed.
    /// </summary>
    public static RouteSpec Refresh() => new(HttpMethod.Post, "api/v1/auth/refresh");

    /// <summary>
    /// <see cref="Services.AuthService.LogoutAsync"/> calls this,
    /// carrying the stored refresh token, so Identity invalidates it server-side
    /// (<c>AgendaBuddy.Identity/Program.cs:196</c>) instead of leaving it valid for its full 24-hour lifetime.
    /// </summary>
    public static RouteSpec Logout() => new(HttpMethod.Post, "api/v1/auth/logout");

    /// <summary>
    /// Always answers 202 regardless of whether the address matched an account (anti-enumeration,
    /// AuthModule.cs) — the client cannot and should not distinguish "email sent" from "no such account".
    /// </summary>
    public static RouteSpec RequestPasswordReset() => new(HttpMethod.Post, "api/v1/auth/password-reset/request");

    public static RouteSpec ConfirmPasswordReset() => new(HttpMethod.Post, "api/v1/auth/password-reset/confirm");
}
