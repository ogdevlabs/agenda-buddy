using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AgendaBuddy.Identity.Requests;
using AgendaBuddy.Identity.Services;

namespace AgendaBuddy.Identity.Modules;

public class AuthModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("api/v1/auth")
            .WithTags("IdentityAPI")
            .WithOpenApi();

        // Re-bound here rather than resolved from DI: Program.cs binds the same section into a local
        // var at builder time (needed before builder.Build() to decide whether to register the
        // limiter at all), and this module needs the identical decision at route-mapping time.
        var rateLimiting = new RateLimitingOptions();
        app.ServiceProvider.GetRequiredService<IConfiguration>()
            .GetSection(RateLimitingOptions.Section).Bind(rateLimiting);

        // Applied to `register` and `login` only, and only when the limiter is registered — RequireRateLimiting
        // with no registered policy throws at request time, so the two conditions have to agree. `refresh` and
        // `logout` stay unlimited: neither spends BCrypt, and throttling refresh would break the hourly
        // rotation a legitimate mobile client performs (D-4).
        var register = auth.MapPost("/register", async (RegisterRequest req, IdentityService svc) =>
        {
            var emailValidator = new System.ComponentModel.DataAnnotations.EmailAddressAttribute();
            if (string.IsNullOrWhiteSpace(req.Email) || !emailValidator.IsValid(req.Email))
                return Results.BadRequest(new { error = "validation_error", message = "Invalid email format." });
            if (req.Password is null || string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 8)
                return Results.BadRequest(new { error = "validation_error", message = "Password must be at least 8 characters." });
            if (req.Role is not "Provider" and not "Customer")
                return Results.BadRequest(new { error = "validation_error", message = "Role must be 'Provider' or 'Customer'." });

            try
            {
                var result = await svc.RegisterAsync(req.Email, req.Password, req.Role);
                return Results.Created("/api/v1/auth/register", new { accessToken = result!.AccessToken, refreshToken = result.RefreshToken });
            }
            catch (AuthValidationException ex) { return Results.BadRequest(new { error = "validation_error", message = ex.Message }); }
            catch (ConflictException ex) { return Results.Conflict(new { error = "conflict", message = ex.Message }); }
            catch (ServiceUnavailableException ex) { return Results.Problem(detail: ex.Message, statusCode: 503, title: "service_unavailable"); }
        }).WithName("Register");

        var login = auth.MapPost("/login", async (LoginRequest req, IdentityService svc) =>
        {
            try
            {
                var result = await svc.LoginAsync(req.Email, req.Password);
                return Results.Ok(new { accessToken = result!.AccessToken, refreshToken = result.RefreshToken });
            }
            catch (UnauthorizedException) { return Results.Unauthorized(); }
            catch (PasswordResetRequiredException ex) { return Results.Problem(detail: ex.Message, statusCode: 403, title: "password_reset_required"); }
            catch (ServiceUnavailableException ex) { return Results.Problem(detail: ex.Message, statusCode: 503, title: "service_unavailable"); }
        }).WithName("Login");

        if (rateLimiting.Enabled)
        {
            register.RequireRateLimiting(RateLimitingOptions.PolicyName);
            login.RequireRateLimiting(RateLimitingOptions.PolicyName);
        }

        auth.MapPost("/refresh", async (RefreshRequest req, IdentityService svc) =>
        {
            if (string.IsNullOrWhiteSpace(req.RefreshToken))
                return Results.Unauthorized();
            try
            {
                var result = await svc.RefreshAsync(req.RefreshToken);
                return Results.Ok(new { accessToken = result!.AccessToken, refreshToken = result.RefreshToken });
            }
            catch (UnauthorizedException) { return Results.Unauthorized(); }
            catch (ServiceUnavailableException ex) { return Results.Problem(detail: ex.Message, statusCode: 503, title: "service_unavailable"); }
        }).WithName("RefreshToken");

        auth.MapPost("/logout", async (LogoutRequest req, IdentityService svc) =>
        {
            if (string.IsNullOrWhiteSpace(req.RefreshToken))
                return Results.NoContent();
            try
            {
                await svc.LogoutAsync(req.RefreshToken, req.AccessToken);
                return Results.NoContent();
            }
            catch (ServiceUnavailableException ex) { return Results.Problem(detail: ex.Message, statusCode: 503, title: "service_unavailable"); }
        }).WithName("Logout");

        // Always 202, whether or not the address matched an account — anti-enumeration, same principle as
        // /login's constant-time dummy hash. Unlimited, same reasoning as /refresh: it spends no BCrypt.
        auth.MapPost("/password-reset/request", async (PasswordResetRequestRequest req, IdentityService svc) =>
        {
            try
            {
                await svc.RequestPasswordResetAsync(req.Email);
            }
            catch (ServiceUnavailableException ex) { return Results.Problem(detail: ex.Message, statusCode: 503, title: "service_unavailable"); }
            return Results.Accepted();
        }).WithName("RequestPasswordReset");

        auth.MapPost("/password-reset/confirm", async (PasswordResetConfirmRequest req, IdentityService svc) =>
        {
            try
            {
                await svc.ConfirmPasswordResetAsync(req.Email, req.Token, req.NewPassword);
                return Results.NoContent();
            }
            catch (AuthValidationException ex) { return Results.BadRequest(new { error = "validation_error", message = ex.Message }); }
            catch (UnauthorizedException ex) { return Results.Problem(detail: ex.Message, statusCode: 401, title: "unauthorized"); }
            catch (ServiceUnavailableException ex) { return Results.Problem(detail: ex.Message, statusCode: 503, title: "service_unavailable"); }
        }).WithName("ConfirmPasswordReset");

        // The credential half of account deletion (App Review Guideline 5.1.1(v)). Takes NO email: the account
        // is the caller's own sub claim, so there is nothing here for a caller to substitute in order to delete
        // somebody else — the same reasoning as DELETE /device-token and Customer's POST /notifications/read-all.
        //
        // 204 whether or not a credential matched, because a deletion that answered differently for a known and
        // an unknown address would be an enumeration oracle. The domain profile is deleted separately by
        // DELETE /api/v1/{customers|providers}/{email}, which the client calls FIRST: this credential is what
        // authorises that call.
        auth.MapDelete("/account", async (ClaimsPrincipal user, HttpRequest request, IdentityService svc) =>
        {
            var email = user.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);

            if (string.IsNullOrWhiteSpace(email))
                return Results.Unauthorized();

            try
            {
                // The caller's own access token, so it stops working the moment the account is gone rather than
                // staying valid for the rest of its 60-minute lifetime. Read from the header the caller already
                // sent, not from a body — /logout takes it in the body only because it also takes a refresh token.
                await svc.DeleteAccountAsync(email, ReadBearerToken(request));
                return Results.NoContent();
            }
            catch (ServiceUnavailableException ex) { return Results.Problem(detail: ex.Message, statusCode: 503, title: "service_unavailable"); }
        }).RequireAuthorization().WithName("DeleteAccount");

        // Not required for login (see CredentialEntity.EmailVerified's own remarks) — confirms
        // ownership of the registered address, an informational/UX signal only.
        auth.MapPost("/register/confirm", async (EmailConfirmRequest req, IdentityService svc) =>
        {
            try
            {
                await svc.ConfirmEmailAsync(req.Email, req.Token);
                return Results.NoContent();
            }
            catch (UnauthorizedException ex) { return Results.Problem(detail: ex.Message, statusCode: 401, title: "unauthorized"); }
            catch (ServiceUnavailableException ex) { return Results.Problem(detail: ex.Message, statusCode: 503, title: "service_unavailable"); }
        }).WithName("ConfirmEmail");
    }

    /// <summary>The bearer token from the Authorization header, or <c>null</c> when the header is absent or not one.</summary>
    private static string? ReadBearerToken(HttpRequest request)
    {
        var header = request.Headers.Authorization.ToString();

        return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? header["Bearer ".Length..].Trim()
            : null;
    }
}
