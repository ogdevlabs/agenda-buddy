using System.Net;
using System.Net.Http.Json;
using AgendaBuddy.Identity.Services;
using AgendaBuddy.Library.Entities;
using AgendaBuddy.MobileApp.Routing;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AgendaBuddy.IntegrationTests.Harness;

/// <summary>
/// Calling logout invokes the server-side logout endpoint, and the
/// previously-valid refresh token is rejected on a subsequent refresh attempt — proved end-to-end
/// against a real, running Identity service. Uses <see cref="AgendaBuddy.MobileApp.Routing.AuthRouteBuilder"/>'s own
/// <c>Logout()</c>/<c>Refresh()</c> route specs, the same way <see cref="MobileClientRouteResolutionTest"/>
/// exercises other <c>*RouteBuilder</c> classes, so this proves the client's actual route/verb/body — not
/// a hand-typed path string that could drift from it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the credential is seeded directly, not through <c>POST /register</c> or <c>/login</c>.</b> Same
/// reason as <see cref="AuthRateLimitTest"/>: minting a real access token needs <c>JWT_PRIVATE_KEY</c>, an
/// environment variable <see cref="CryptoSessionFixture"/> deliberately never sets in this public
/// repository. Seeding the credential's refresh-token hash directly with the same
/// <see cref="IdentityService.HashToken"/> Identity itself uses reaches every part of the logout/refresh
/// path this task cares about without a real signature ever existing.
/// </para>
/// <para>
/// <b>The "still valid" baseline cannot itself be a <c>/refresh</c> call.</b>
/// <c>IdentityService.RefreshAsync</c> (<c>AgendaBuddy.Identity/Services/IdentityService.cs:253-258</c>) rotates the
/// stored hash to a brand-new one on every <i>matched</i> attempt — before it ever checks for a signing
/// key — so spending one to "prove the token is still live" would consume it and invalidate the very
/// thing the rest of the test is about. <see cref="Refresh_WithAValidUnexpiredToken_MatchesTheCredential"/>
/// proves the matched-but-unsigned-key path returns 500 (not 401) using its own, disposable token; the
/// main test below establishes "previously valid" directly against the seeded document instead, using the
/// exact fields <c>RefreshAsync</c>'s own filter checks (matching hash, unexpired, unlocked).
/// </para>
/// </remarks>
[Collection(HarnessCollection.Name)]
public class LogoutTest(ServiceHostFixture<IdentityAnchor> host)
    : IClassFixture<ServiceHostFixture<IdentityAnchor>>
{
    private const string Email = "logout-test@example.com";
    private const string RawRefreshToken = "logout-test-refresh-token";
    private const string ControlEmail = "logout-control@example.com";
    private const string ControlRawRefreshToken = "logout-control-refresh-token";

    private static async Task SeedCredentialWithRefreshTokenAsync(
        ServiceHost service, string email, string rawRefreshToken)
    {
        await service.Database.GetCollection<CredentialEntity>("credentials").InsertOneAsync(
            new CredentialEntity
            {
                Id = ObjectId.GenerateNewId().ToString(),
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("irrelevant-for-this-test", workFactor: 12),
                Role = "Provider",
                RefreshToken = new RefreshTokenDocument
                {
                    Hash = IdentityService.HashToken(rawRefreshToken),
                    Expiry = DateTime.UtcNow.AddHours(24)
                }
            });
    }

    private static async Task<CredentialEntity?> StoredAsync(ServiceHost service, string email) =>
        await service.Database.GetCollection<CredentialEntity>("credentials")
            .Find(Builders<CredentialEntity>.Filter.Eq(credential => credential.Email, email))
            .SingleOrDefaultAsync();

    private static HttpRequestMessage BuildLogoutRequest(string refreshToken)
    {
        var route = AuthRouteBuilder.Logout();
        return new HttpRequestMessage(route.Method, route.Path)
        {
            Content = JsonContent.Create(new { refreshToken })
        };
    }

    private static HttpRequestMessage BuildRefreshRequest(string refreshToken)
    {
        var route = AuthRouteBuilder.Refresh();
        return new HttpRequestMessage(route.Method, route.Path)
        {
            Content = JsonContent.Create(new { refreshToken })
        };
    }

    [Fact]
    public async Task Logout_ThenRefresh_TheOldRefreshTokenIsRejected()
    {
        using var service = host.StartService();
        await SeedCredentialWithRefreshTokenAsync(service, Email, RawRefreshToken);

        // "Previously valid": the seeded document is live by the exact fields RefreshAsync's own
        // filter checks (matching hash, unexpired, unlocked) — asserted directly, not via a /refresh
        // call, which would rotate (and so consume) the very token this test is about. See the class
        // remarks and Refresh_WithAValidUnexpiredToken_MatchesTheCredential below for that proof.
        var seeded = await StoredAsync(service, Email);
        Assert.Equal(IdentityService.HashToken(RawRefreshToken), seeded!.RefreshToken!.Hash);
        Assert.True(seeded.RefreshToken.Expiry > DateTime.UtcNow);
        Assert.Null(seeded.LockUntil);

        var logoutResponse = await service.Client.SendAsync(BuildLogoutRequest(RawRefreshToken));
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        // The credential's embedded refresh_token sub-document is gone (IdentityService.LogoutAsync
        // $unsets it), which is what the 401 below is actually proving.
        var storedAfterLogout = await StoredAsync(service, Email);
        Assert.NotNull(storedAfterLogout);
        Assert.Null(storedAfterLogout!.RefreshToken);

        // The AC11 claim itself: a subsequent refresh attempt with the now-logged-out token is
        // rejected, over real HTTP, through the client's own AuthRouteBuilder.Refresh() route.
        var afterLogout = await service.Client.SendAsync(BuildRefreshRequest(RawRefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithAValidUnexpiredToken_MatchesTheCredential()
    {
        // Control for the test above: absent logout, the same seed shape is matched by the lookup
        // (500 here is the missing-JWT_PRIVATE_KEY misconfiguration, reached only AFTER a successful
        // match — see the class remarks). Uses its own credential/token so running this does not
        // consume the one the main test relies on.
        using var service = host.StartService();
        await SeedCredentialWithRefreshTokenAsync(service, ControlEmail, ControlRawRefreshToken);

        var response = await service.Client.SendAsync(BuildRefreshRequest(ControlRawRefreshToken));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task Logout_WithATokenNoCredentialHolds_IsStillNoContent()
    {
        // AgendaBuddy.Identity/Program.cs:196's logout route is deliberately idempotent, per
        // IdentityService.LogoutAsync's own doc comment — logging out twice, or logging out a token
        // nobody holds, must not leak whether an account exists.
        using var service = host.StartService();

        var response = await service.Client.SendAsync(BuildLogoutRequest("no-such-refresh-token"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}
