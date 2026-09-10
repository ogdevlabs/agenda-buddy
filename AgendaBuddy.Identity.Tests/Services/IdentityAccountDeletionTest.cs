using AgendaBuddy.Identity.Services;
using AgendaBuddy.Identity.Tests.Helpers;
using AgendaBuddy.Library.Entities;
using AgendaBuddy.Library.Services;
using MongoDB.Bson;
using Moq;
using Xunit;

namespace AgendaBuddy.Identity.Tests.Services;

/// <summary>
/// Deleting the credential half of an account (App Review Guideline 5.1.1(v)).
/// </summary>
/// <remarks>
/// The domain profile lives in a different database and is removed by
/// <c>DELETE /api/v1/{customers|providers}/{email}</c>, which the client calls <b>first</b> — this credential is
/// what authorises that call.
/// </remarks>
public class IdentityAccountDeletionTest
{
    private const string Email = "ada@example.com";
    private const string Password = "correct horse battery";

    private readonly FakeDateTimeProvider _clock =
        new(new DateTime(2026, 9, 8, 12, 0, 0, DateTimeKind.Utc));

    private readonly InMemoryCredentialRepository _repo = new();
    private readonly Mock<IDeviceTokenService> _deviceTokens = new();
    private readonly FakeTokenRevocationStore _revocations = new();
    private readonly IdentityService _svc;

    public IdentityAccountDeletionTest()
    {
        var (_, privateKeyPem) = RsaKeyHelper.GenerateTestKeyPair();
        Environment.SetEnvironmentVariable("JWT_PRIVATE_KEY", privateKeyPem);

        _svc = new IdentityService(
            _repo,
            _clock,
            tokenRevocationStore: _revocations,
            deviceTokenService: _deviceTokens.Object);
    }

    private async Task<string> RegisterAsync(string email = Email)
    {
        var tokens = await IdentityTestSession.RegisterConfirmedAsync(_svc, email, Password, "Customer");
        return tokens.AccessToken;
    }

    [Fact]
    public async Task DeletingAnAccountRemovesTheCredential()
    {
        await RegisterAsync();

        await _svc.DeleteAccountAsync(Email);

        Assert.Null(await _repo.FindOneAsync(new BsonDocument("email", Email)));
    }

    /// <summary>The point of the whole operation: after it, the password no longer works.</summary>
    [Fact]
    public async Task AfterDeletionTheCredentialNoLongerAuthenticates()
    {
        await RegisterAsync();

        await _svc.DeleteAccountAsync(Email);

        await Assert.ThrowsAsync<UnauthorizedException>(() => _svc.LoginAsync(Email, Password));
    }

    /// <summary>
    /// ⚠️ <b>Only the named account.</b> A delete that reached wider than its filter would be the most damaging
    /// possible bug here, and the in-memory double's <c>DeleteManyAsync</c> deliberately evaluates the filter
    /// strictly so an unsupported one throws rather than matching everything.
    /// </summary>
    [Fact]
    public async Task DeletingOneAccountLeavesEveryOtherCredentialAlone()
    {
        await RegisterAsync();
        await RegisterAsync("someone.else@example.com");

        await _svc.DeleteAccountAsync(Email);

        Assert.Null(await _repo.FindOneAsync(new BsonDocument("email", Email)));
        Assert.NotNull(await _repo.FindOneAsync(new BsonDocument("email", "someone.else@example.com")));
    }

    /// <summary>
    /// ⚠️ <b>Idempotent, and it never reports whether an account existed.</b> A deletion that answered differently
    /// for a known and an unknown address would be an enumeration oracle on a route every authenticated caller can
    /// reach — the same reasoning that makes <c>DELETE /device-token</c> answer 204 either way.
    /// </summary>
    [Fact]
    public async Task DeletingAnAccountThatDoesNotExistIsNotAnError()
    {
        await _svc.DeleteAccountAsync("never.registered@example.com");
    }

    [Fact]
    public async Task DeletingTwiceIsNotAnError()
    {
        await RegisterAsync();

        await _svc.DeleteAccountAsync(Email);
        await _svc.DeleteAccountAsync(Email);
    }

    /// <summary>
    /// The address is lower-cased on the way in, matching registration — otherwise a caller whose claim carries
    /// different casing would delete nothing and be told it worked.
    /// </summary>
    [Fact]
    public async Task TheAddressIsNormalisedTheSameWayRegistrationNormalisesIt()
    {
        await RegisterAsync();

        await _svc.DeleteAccountAsync("ADA@EXAMPLE.COM");

        Assert.Null(await _repo.FindOneAsync(new BsonDocument("email", Email)));
    }

    /// <summary>
    /// The device registration is released, and <b>before</b> the credential — a fault between the two leaves an
    /// account that can still sign in rather than a deleted one whose device keeps receiving push.
    /// </summary>
    [Fact]
    public async Task TheDeviceRegistrationIsReleased()
    {
        await RegisterAsync();

        await _svc.DeleteAccountAsync(Email);

        _deviceTokens.Verify(d => d.DeleteByEmailAsync(Email), Times.Once);
    }

    /// <summary>
    /// ⚠️ <b>A push-table failure must not block the deletion.</b> An account that cannot be deleted because a
    /// device-token collection was briefly unreachable is a worse outcome than a stale token row — and the
    /// credential delete is what actually makes sign-in stop.
    /// </summary>
    [Fact]
    public async Task AFailureReleasingTheDeviceRegistrationDoesNotBlockTheDeletion()
    {
        _deviceTokens.Setup(d => d.DeleteByEmailAsync(It.IsAny<string>()))
                     .ThrowsAsync(new MongoDB.Driver.MongoException("device tokens unreachable"));
        await RegisterAsync();

        await _svc.DeleteAccountAsync(Email);

        Assert.Null(await _repo.FindOneAsync(new BsonDocument("email", Email)));
    }

    /// <summary>
    /// The caller's own access token is denylisted, so it stops working immediately instead of staying valid for
    /// the rest of its 60-minute lifetime against an account that no longer exists.
    /// </summary>
    [Fact]
    public async Task TheCallersAccessTokenIsRevoked()
    {
        var accessToken = await RegisterAsync();

        await _svc.DeleteAccountAsync(Email, accessToken);

        Assert.Single(_revocations.Revoked);
    }

    /// <summary>A garbage or absent token is skipped rather than failing the deletion.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-jwt")]
    public async Task AnUnusableAccessTokenDoesNotBlockTheDeletion(string? accessToken)
    {
        await RegisterAsync();

        await _svc.DeleteAccountAsync(Email, accessToken);

        Assert.Null(await _repo.FindOneAsync(new BsonDocument("email", Email)));
    }

    /// <summary>Works without one wired, so the deletion path does not depend on push being configured.</summary>
    [Fact]
    public async Task DeletionWorksWithNoDeviceTokenServiceWired()
    {
        var (_, privateKeyPem) = RsaKeyHelper.GenerateTestKeyPair();
        Environment.SetEnvironmentVariable("JWT_PRIVATE_KEY", privateKeyPem);

        var repo = new InMemoryCredentialRepository();
        var svc = new IdentityService(repo, _clock);
        await svc.RegisterAsync(Email, Password, "Customer");

        await svc.DeleteAccountAsync(Email);

        Assert.Null(await repo.FindOneAsync(new BsonDocument("email", Email)));
    }
}
