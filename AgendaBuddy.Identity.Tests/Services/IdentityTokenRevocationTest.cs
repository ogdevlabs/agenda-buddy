using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading.Tasks;
using AgendaBuddy.Identity.Services;
using AgendaBuddy.Identity.Tests.Helpers;
using Xunit;

namespace AgendaBuddy.Identity.Tests.Services;

/// <summary>
/// F-023: logging out denylists the caller's own access token, so it stops working before its
/// remaining lifetime elapses, instead of only clearing the refresh token.
/// </summary>
[Collection("Sequential")]
public class IdentityTokenRevocationTest : IDisposable
{
    private const string Email = "revoke-me@example.com";
    private const string Password = "password123";

    private readonly InMemoryCredentialRepository _repo = new();
    private readonly FakeDateTimeProvider _clock = new(new DateTime(2026, 8, 27, 12, 0, 0, DateTimeKind.Utc));
    private readonly FakeTokenRevocationStore _revocationStore = new();
    private readonly IdentityService _svc;

    public IdentityTokenRevocationTest()
    {
        var (_, privateKeyPem) = RsaKeyHelper.GenerateTestKeyPair();
        Environment.SetEnvironmentVariable("JWT_PRIVATE_KEY", privateKeyPem);

        _svc = new IdentityService(_repo, _clock, tokenRevocationStore: _revocationStore);
    }

    public void Dispose() => Environment.SetEnvironmentVariable("JWT_PRIVATE_KEY", null);

    private static string JtiOf(string accessToken) =>
        new JwtSecurityTokenHandler().ReadJwtToken(accessToken)
            .Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;

    [Fact]
    public async Task Logout_WithAnAccessToken_RevokesItsJti()
    {
        var tokens = await _svc.RegisterAsync(Email, Password, "Provider");

        await _svc.LogoutAsync(tokens!.RefreshToken, tokens.AccessToken);

        Assert.True(await _revocationStore.IsRevokedAsync(JtiOf(tokens.AccessToken)));
    }

    [Fact]
    public async Task Logout_WithNoAccessToken_RevokesNothing()
    {
        var tokens = await _svc.RegisterAsync(Email, Password, "Provider");

        await _svc.LogoutAsync(tokens!.RefreshToken);

        Assert.Empty(_revocationStore.Revoked);
    }

    [Fact]
    public async Task Logout_WithAGarbageAccessToken_DoesNotThrowAndRevokesNothing()
    {
        var tokens = await _svc.RegisterAsync(Email, Password, "Provider");

        await _svc.LogoutAsync(tokens!.RefreshToken, "not-a-real-jwt");

        Assert.Empty(_revocationStore.Revoked);
    }
}
