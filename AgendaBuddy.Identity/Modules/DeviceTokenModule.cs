using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AgendaBuddy.Identity.Requests;
using AgendaBuddy.Identity.Services;

namespace AgendaBuddy.Identity.Modules;

public class DeviceTokenModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/device-token", async (RegisterDeviceTokenRequest request, ClaimsPrincipal user, IDeviceTokenService svc) =>
        {
            if (request is null || string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.Platform))
                return Results.BadRequest(new { error = "validation_error", message = "Token and platform are required." });

            if (request.Platform is not "android" and not "ios")
                return Results.BadRequest(new { error = "validation_error", message = "Platform must be 'android' or 'ios'." });

            var email = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                        ?? user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

            if (string.IsNullOrWhiteSpace(email))
                return Results.Unauthorized();

            await svc.UpsertAsync(email, request.Token, request.Platform);
            return Results.Ok();
        }).RequireAuthorization().WithName("RegisterDeviceToken");

        // Sign-out. Takes no body: the account comes from the caller's own token, so there is nothing here for
        // a caller to substitute — the same reasoning as Customer's POST /notifications/read-all.
        // 204 whether or not a registration existed: "this device is not addressable for this account" is the
        // requested state either way, and answering 404 would tell a caller whether a device was registered.
        app.MapDelete("/device-token", async (ClaimsPrincipal user, IDeviceTokenService svc) =>
        {
            var email = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                        ?? user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

            if (string.IsNullOrWhiteSpace(email))
                return Results.Unauthorized();

            await svc.DeleteByEmailAsync(email);
            return Results.NoContent();
        }).RequireAuthorization().WithName("UnregisterDeviceToken");
    }
}
