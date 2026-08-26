using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using AgendaBuddy.IntegrationTests.Harness;
using Identity.Services;
using Library.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AgendaBuddy.IntegrationTests.Persistence;

/// <summary>
/// F-018-T12 / AC-8. Identity gets tier 2 across ALL FIVE write endpoints — register, login, refresh,
/// logout, device-token — not just one, because it is the most security-critical write surface in the
/// system and the original tier matrix wrongly scoped it to route-contract only.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why <c>JWT_PRIVATE_KEY</c> is set here, unlike every other harness test that touches Identity.</b>
/// <see cref="CryptoSessionFixture"/> deliberately never materialises a private-key PEM string, because
/// its keypair backs <c>JWT_PUBLIC_KEY</c> for the whole session — every service's authentication
/// middleware trusts it for the run's entire duration, so a PEM of its private half would be a much
/// larger blast radius than one throwaway key. Register/login/refresh mint an access token
/// (<c>IdentityService.CreateAccessToken</c>) purely to put it in the HTTP response; this test never
/// authenticates with it, so a private key generated fresh here, used only for the duration of one test
/// method, and never written anywhere but this process's environment, carries none of that risk. It is
/// restored to its prior value (unset) in every case, including on assertion failure, so it cannot leak
/// into another test in this sequential collection (<see cref="HarnessCollection"/>) — notably
/// <c>LogoutTest.Refresh_WithAValidUnexpiredToken_MatchesTheCredential</c>, whose 500 assertion is the
/// control for this exact key being ABSENT.
/// </para>
/// <para>
/// <b>Why register/login/refresh/logout are called directly, unlike device-token.</b> None of the four
/// require authentication (<c>Identity/Program.cs:147,167,184,197</c> — no <c>RequireAuthorization()</c>);
/// device-token does, and <see cref="TokenFactory"/> — signed with the session's own key — is what proves
/// that route without needing <c>JWT_PRIVATE_KEY</c> at all.
/// </para>
/// </remarks>
[Collection(HarnessCollection.Name)]
public class IdentityPersistenceTest(ServiceHostFixture<IdentityAnchor> host, CryptoSessionFixture crypto)
    : IClassFixture<ServiceHostFixture<IdentityAnchor>>
{
    private const string PrivateKeyEnvVar = "JWT_PRIVATE_KEY";
    private const string RegisterEmail = "identity-round-trip@example.com";
    private const string Password = "RoundTrip123!";

    private readonly TokenFactory _tokens = new(crypto);

    private static string GenerateThrowawayPrivateKeyPem()
    {
        using var rsa = RSA.Create(2048);
        return "-----BEGIN RSA PRIVATE KEY-----\n"
               + Convert.ToBase64String(rsa.ExportRSAPrivateKey(), Base64FormattingOptions.InsertLineBreaks)
               + "\n-----END RSA PRIVATE KEY-----";
    }

    private static async Task<CredentialEntity?> StoredCredentialAsync(ServiceHost service, string email) =>
        await ConfiguredCollection.Of<CredentialEntity>(service, "CollectionName", "credentials")
            .Find(Builders<CredentialEntity>.Filter.Eq(c => c.Email, email))
            .SingleOrDefaultAsync();

    private static (string AccessToken, string RefreshToken) ParseTokenPair(string body)
    {
        using var document = JsonDocument.Parse(body);
        return (
            document.RootElement.GetProperty("accessToken").GetString()!,
            document.RootElement.GetProperty("refreshToken").GetString()!);
    }

    [Fact]
    public async Task AC8_RegisterLoginRefreshLogout_EachStepPersistsAndTheNextStepReadsItBack()
    {
        using var service = host.StartService();
        var originalPrivateKey = Environment.GetEnvironmentVariable(PrivateKeyEnvVar);

        try
        {
            Environment.SetEnvironmentVariable(PrivateKeyEnvVar, GenerateThrowawayPrivateKeyPem());

            // ── register ────────────────────────────────────────────────────────────────────────────
            var registerResponse = await service.Client.PostAsJsonAsync("api/v1/auth/register", new
            {
                Email = RegisterEmail,
                Password = Password,
                Role = "Provider",
            });
            Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
            var (_, registerRefreshToken) = ParseTokenPair(await registerResponse.Content.ReadAsStringAsync());

            var afterRegister = await StoredCredentialAsync(service, RegisterEmail);
            Assert.NotNull(afterRegister);
            Assert.Equal(RegisterEmail, afterRegister.Email);
            Assert.Equal("Provider", afterRegister.Role);
            Assert.False(afterRegister.MustResetPassword);
            Assert.StartsWith("$2", afterRegister.PasswordHash);
            Assert.NotNull(afterRegister.RefreshToken);
            Assert.Equal(IdentityService.HashToken(registerRefreshToken), afterRegister.RefreshToken!.Hash);
            Assert.True(afterRegister.RefreshToken.Expiry > DateTime.UtcNow);

            // ── login ───────────────────────────────────────────────────────────────────────────────
            var loginResponse = await service.Client.PostAsJsonAsync("api/v1/auth/login", new
            {
                Email = RegisterEmail,
                Password = Password,
            });
            Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
            var (_, loginRefreshToken) = ParseTokenPair(await loginResponse.Content.ReadAsStringAsync());

            var afterLogin = await StoredCredentialAsync(service, RegisterEmail);
            Assert.NotNull(afterLogin);
            // Rotated: login's stored hash is the NEW token's, and no longer register's.
            Assert.Equal(IdentityService.HashToken(loginRefreshToken), afterLogin.RefreshToken!.Hash);
            Assert.NotEqual(IdentityService.HashToken(registerRefreshToken), afterLogin.RefreshToken.Hash);
            Assert.Equal(0, afterLogin.FailedAttempts);

            // ── refresh ─────────────────────────────────────────────────────────────────────────────
            var refreshResponse = await service.Client.PostAsJsonAsync(
                "api/v1/auth/refresh", new { RefreshToken = loginRefreshToken });
            Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
            var (_, refreshedRefreshToken) = ParseTokenPair(await refreshResponse.Content.ReadAsStringAsync());

            var afterRefresh = await StoredCredentialAsync(service, RegisterEmail);
            Assert.NotNull(afterRefresh);
            Assert.Equal(IdentityService.HashToken(refreshedRefreshToken), afterRefresh.RefreshToken!.Hash);
            Assert.NotEqual(IdentityService.HashToken(loginRefreshToken), afterRefresh.RefreshToken.Hash);

            // ── logout ──────────────────────────────────────────────────────────────────────────────
            var logoutResponse = await service.Client.PostAsJsonAsync(
                "api/v1/auth/logout", new { RefreshToken = refreshedRefreshToken });
            Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

            var afterLogout = await StoredCredentialAsync(service, RegisterEmail);
            Assert.NotNull(afterLogout);
            Assert.Null(afterLogout.RefreshToken);

            // Closes the loop through a fresh live request, not just this test's own direct query: the
            // now-logged-out token is rejected by the real /refresh route.
            var refreshAfterLogout = await service.Client.PostAsJsonAsync(
                "api/v1/auth/refresh", new { RefreshToken = refreshedRefreshToken });
            Assert.Equal(HttpStatusCode.Unauthorized, refreshAfterLogout.StatusCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable(PrivateKeyEnvVar, originalPrivateKey);
        }
    }

    [Fact]
    public async Task AC8_ADeviceTokenRegistration_ReadsBackFromTheDeviceTokensCollection()
    {
        using var service = host.StartService();
        const string Email = "device-token-round-trip@example.com";
        const string Token = "a-real-device-push-token";
        const string Platform = "ios";

        var request = new HttpRequestMessage(HttpMethod.Post, "/device-token")
        {
            Content = JsonContent.Create(new { Token, Platform }),
            Headers =
            {
                Authorization = new AuthenticationHeaderValue(
                    "Bearer", _tokens.CreateToken(Email, TokenFactory.ProviderRole)),
            },
        };

        var response = await service.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // "device_tokens" is hardcoded in production (Identity/Extensions/ServiceCollectionExtension.cs) —
        // the ONE collection name in the whole system that is not config-driven (05-data-model.md) — so
        // matching that literal here reflects the real source of truth rather than working around it.
        var stored = await service.Database.GetCollection<DeviceTokenEntity>("device_tokens")
            .Find(Builders<DeviceTokenEntity>.Filter.Eq(d => d.UserEmail, Email))
            .SingleOrDefaultAsync();

        Assert.NotNull(stored);
        Assert.Equal(Email, stored.UserEmail);
        Assert.Equal(Token, stored.Token);
        Assert.Equal(Platform, stored.Platform);
        Assert.True(stored.RegisteredAt > DateTime.UtcNow.AddMinutes(-1));
        Assert.True(stored.UpdatedAt > DateTime.UtcNow.AddMinutes(-1));
    }
}
