using System;
using System.Threading.Tasks;
using AgendaBuddy.Identity.Services;
using AgendaBuddy.Identity.Tests.Helpers;
using AgendaBuddy.Library.Entities;
using AgendaBuddy.Library.Services;
using MongoDB.Bson;
using Xunit;

namespace AgendaBuddy.Identity.Tests.Services;

/// <summary>
/// F-022: a password-reset flow where none existed before. <c>MustResetPassword</c> now blocks a normal
/// login, and a single-use, short-lived reset token (mirroring the refresh-token pattern) lets the
/// account holder set a new password without one.
/// </summary>
[Collection("Sequential")]
public class IdentityPasswordResetTest : IDisposable
{
    private const string Email = "resetme@example.com";
    private const string Password = "password123";

    private readonly FakeDateTimeProvider _clock =
        new(new DateTime(2026, 8, 27, 12, 0, 0, DateTimeKind.Utc));

    private readonly InMemoryCredentialRepository _repo = new();
    private readonly IdentityService _svc;

    public IdentityPasswordResetTest()
    {
        var (_, privateKeyPem) = RsaKeyHelper.GenerateTestKeyPair();
        Environment.SetEnvironmentVariable("JWT_PRIVATE_KEY", privateKeyPem);

        _svc = new IdentityService(_repo, _clock);
    }

    public void Dispose() => Environment.SetEnvironmentVariable("JWT_PRIVATE_KEY", null);

    private async Task<CredentialEntity> Stored() => Assert.Single(await _repo.GetAllAsync());

    [Fact]
    public async Task RequestPasswordReset_ForAnExistingAccount_StoresAHashedSingleUseToken()
    {
        await _svc.RegisterAsync(Email, Password, "Provider");

        var token = await _svc.RequestPasswordResetAsync(Email);

        Assert.NotNull(token);
        var stored = await Stored();
        Assert.NotNull(stored.ResetToken);
        Assert.Equal(IdentityService.HashToken(token!), stored.ResetToken!.Hash);
        Assert.Equal(_clock.UtcNow.AddMinutes(30), stored.ResetToken.Expiry);
        // The raw token is never the hash — the whole point of storing only the hash.
        Assert.NotEqual(token, stored.ResetToken.Hash);
    }

    [Fact]
    public async Task RequestPasswordReset_ForAnUnknownAddress_WritesNothingAndReturnsNull()
    {
        var token = await _svc.RequestPasswordResetAsync("nobody@example.com");

        Assert.Null(token);
        Assert.Empty(await _repo.GetAllAsync());
    }

    [Fact]
    public async Task RequestPasswordReset_SendsAnInAppNotification_WhenANotifierIsConfigured()
    {
        var notifier = new RecordingNotificationService();
        var svc = new IdentityService(_repo, _clock, notificationService: notifier);
        await svc.RegisterAsync(Email, Password, "Provider");
        notifier.Sent.Clear(); // Registration itself sends an email-confirmation notification — not under test here.

        await svc.RequestPasswordResetAsync(Email);

        var sent = Assert.Single(notifier.Sent);
        Assert.Equal(Email, sent.RecipientEmail);
        Assert.Equal(NotificationType.PasswordResetRequested, sent.Type);
    }

    [Fact]
    public async Task ConfirmPasswordReset_WithAValidToken_SetsTheNewPasswordAndEndsExistingSessions()
    {
        await _svc.RegisterAsync(Email, Password, "Provider");
        await _svc.LoginAsync(Email, Password); // establishes a refresh_token — must not survive a reset
        var token = await _svc.RequestPasswordResetAsync(Email);

        await _svc.ConfirmPasswordResetAsync(Email, token!, "brand-new-password");

        var stored = await Stored();
        Assert.Null(stored.ResetToken);
        Assert.Null(stored.RefreshToken);
        Assert.False(stored.MustResetPassword);
        Assert.NotNull(await _svc.LoginAsync(Email, "brand-new-password"));
        await Assert.ThrowsAsync<UnauthorizedException>(() => _svc.LoginAsync(Email, Password));
    }

    [Fact]
    public async Task ConfirmPasswordReset_ClearsAnExistingLockout()
    {
        await _svc.RegisterAsync(Email, Password, "Provider");
        await _repo.FindOneAndUpdateAsync(
            new BsonDocument("email", Email),
            new BsonDocument("$set", new BsonDocument("lock_until", _clock.UtcNow.AddMinutes(15))));
        var token = await _svc.RequestPasswordResetAsync(Email);

        await _svc.ConfirmPasswordResetAsync(Email, token!, "brand-new-password");

        Assert.Null((await Stored()).LockUntil);
    }

    [Fact]
    public async Task ConfirmPasswordReset_WithAWrongToken_IsRejectedAndChangesNothing()
    {
        await _svc.RegisterAsync(Email, Password, "Provider");
        await _svc.RequestPasswordResetAsync(Email);

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _svc.ConfirmPasswordResetAsync(Email, "not-the-real-token", "brand-new-password"));

        Assert.NotNull(await _svc.LoginAsync(Email, Password));
    }

    [Fact]
    public async Task ConfirmPasswordReset_AfterExpiry_IsRejected()
    {
        await _svc.RegisterAsync(Email, Password, "Provider");
        var token = await _svc.RequestPasswordResetAsync(Email);

        _clock.Advance(TimeSpan.FromMinutes(31));

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _svc.ConfirmPasswordResetAsync(Email, token!, "brand-new-password"));
    }

    [Fact]
    public async Task ConfirmPasswordReset_IsSingleUse_ASecondAttemptWithTheSameTokenIsRejected()
    {
        await _svc.RegisterAsync(Email, Password, "Provider");
        var token = await _svc.RequestPasswordResetAsync(Email);
        await _svc.ConfirmPasswordResetAsync(Email, token!, "brand-new-password");

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _svc.ConfirmPasswordResetAsync(Email, token!, "yet-another-password"));
    }

    [Fact]
    public async Task ConfirmPasswordReset_RejectsAPasswordShorterThanEightCharacters()
    {
        await _svc.RegisterAsync(Email, Password, "Provider");
        var token = await _svc.RequestPasswordResetAsync(Email);

        await Assert.ThrowsAsync<AuthValidationException>(
            () => _svc.ConfirmPasswordResetAsync(Email, token!, "short"));

        // The token must still be usable — a rejected attempt did not burn it.
        Assert.NotNull((await Stored()).ResetToken);
    }

    [Fact]
    public async Task Login_ForAnAccountFlaggedForForcedReset_IsBlockedRatherThanIssuingASession()
    {
        await _svc.RegisterAsync(Email, Password, "Provider");
        await _repo.FindOneAndUpdateAsync(
            new BsonDocument("email", Email),
            new BsonDocument("$set", new BsonDocument("must_reset_password", true)));
        var refreshTokenBeforeBlockedLogin = (await Stored()).RefreshToken?.Hash;

        await Assert.ThrowsAsync<PasswordResetRequiredException>(() => _svc.LoginAsync(Email, Password));

        // The blocked attempt must not rotate the session it refused to issue.
        Assert.Equal(refreshTokenBeforeBlockedLogin, (await Stored()).RefreshToken?.Hash);
    }

    private sealed class RecordingNotificationService : INotificationService
    {
        public List<NotificationEntity> Sent { get; } = [];

        public Task SendAsync(NotificationEntity notification)
        {
            Sent.Add(notification);
            return Task.CompletedTask;
        }

        // Identity only ever sends. The read side is answered emptily rather than left unimplemented so a
        // test that starts reading gets an honest empty inbox, not an exception from the double.
        public Task<IEnumerable<NotificationEntity>> GetForRecipientAsync(
            string recipientEmail, int limit = NotificationService.DefaultPageSize, bool unreadOnly = false) =>
            Task.FromResult<IEnumerable<NotificationEntity>>([]);

        public Task<long> CountUnreadAsync(string recipientEmail) => Task.FromResult(0L);

        public Task MarkReadAsync(string notificationId) => Task.CompletedTask;

        public Task<long> MarkAllReadAsync(string recipientEmail) => Task.FromResult(0L);
    }
}
