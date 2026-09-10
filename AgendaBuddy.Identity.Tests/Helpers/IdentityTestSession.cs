using AgendaBuddy.Identity.Requests;
using AgendaBuddy.Identity.Services;

namespace AgendaBuddy.Identity.Tests.Helpers;

internal static class IdentityTestSession
{
    public static async Task<TokenResponse> RegisterConfirmedAsync(
        IdentityService service,
        string email,
        string password,
        string role)
    {
        await ConfirmRegistrationAsync(service, email, password, role);
        return (await service.LoginAsync(email, password))!;
    }

    public static async Task ConfirmRegistrationAsync(
        IdentityService service,
        string email,
        string password,
        string role)
    {
        var registration = await service.RegisterAsync(email, password, role);
        await service.ConfirmEmailAsync(registration.EmailVerificationToken);
    }
}
