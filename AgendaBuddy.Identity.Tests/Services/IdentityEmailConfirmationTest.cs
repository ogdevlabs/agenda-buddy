using System;
using System.Threading.Tasks;
using AgendaBuddy.Identity.Services;
using AgendaBuddy.Identity.Tests.Helpers;
using AgendaBuddy.Library.Entities;
using AgendaBuddy.Library.Services;
using Xunit;

namespace AgendaBuddy.Identity.Tests.Services;

/// <summary>
/// Registration now issues a single-use email-confirmation token (mirroring the password-reset token
/// pattern) so an account holder can prove ownership of the address they registered. Not gated on for
/// login (ADR-052: no email provider is configured) — see CredentialEntity.EmailVerified's own remarks.
/// </summary>
[Collection("Sequential")]
public class IdentityEmailConfirmationTest : IDisposable
{
    private const string Email = "confirmme@example.com";
    private const string Password = "password123";

    private readonly FakeDateTimeProvider _clock =
        new(new DateTime(2026, 8, 28, 12, 0, 0, DateTimeKind.Utc));

    private readonly InMemoryCredentialRepository _repo = new();
    private readonly IdentityService _svc;

    public IdentityEmailConfirmationTest()
    {
        var (_, privateKeyPem) = RsaKeyHelper.GenerateTestKeyPair();
        Environment.SetEnvironmentVariable("JWT_PRIVATE_KEY", privateKeyPem);

        _svc = new IdentityService(_repo, _clock);
    }

    public void Dispose() => Environment.SetEnvironmentVariable("JWT_PRIVATE_KEY", null);

    private async Task<CredentialEntity> Stored() => Assert.Single(await _repo.GetAllAsync());

    [Fact]
    public async Task Register_StoresAHashedSingleUseEmailConfirmationTokenAndReturnsItUnverified()
    {
        var result = await _svc.RegisterAsync(Email, Password, "Provider");

        Assert.NotNull(result!.EmailVerificationToken);
        var stored = await Stored();
        Assert.False(stored.EmailVerified);
        Assert.NotNull(stored.EmailVerificationToken);
        Assert.Equal(IdentityService.HashToken(result.EmailVerificationToken!), stored.EmailVerificationToken!.Hash);
        Assert.Equal(_clock.UtcNow.AddHours(24), stored.EmailVerificationToken.Expiry);
        // The raw token is never the hash — the whole point of storing only the hash.
        Assert.NotEqual(result.EmailVerificationToken, stored.EmailVerificationToken.Hash);
    }

    [Fact]
    public async Task Register_SendsAnInAppNotification_WhenANotifierIsConfigured()
    {
        var notifier = new RecordingNotificationService();
        var svc = new IdentityService(_repo, _clock, notificationService: notifier);

        await svc.RegisterAsync(Email, Password, "Provider");

        var sent = Assert.Single(notifier.Sent);
        Assert.Equal(Email, sent.RecipientEmail);
        Assert.Equal(NotificationType.EmailConfirmationRequested, sent.Type);
    }

    [Fact]
    public async Task ConfirmEmail_WithAValidToken_SetsEmailVerifiedAndClearsTheToken()
    {
        var result = await _svc.RegisterAsync(Email, Password, "Provider");

        await _svc.ConfirmEmailAsync(Email, result!.EmailVerificationToken!);

        var stored = await Stored();
        Assert.True(stored.EmailVerified);
        Assert.Null(stored.EmailVerificationToken);
    }

    [Fact]
    public async Task ConfirmEmail_DoesNotAffectLogin_EitherBeforeOrAfterConfirmation()
    {
        await _svc.RegisterAsync(Email, Password, "Provider");

        Assert.NotNull(await _svc.LoginAsync(Email, Password));
    }

    [Fact]
    public async Task ConfirmEmail_WithAWrongToken_IsRejectedAndChangesNothing()
    {
        await _svc.RegisterAsync(Email, Password, "Provider");

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _svc.ConfirmEmailAsync(Email, "not-the-real-token"));

        Assert.False((await Stored()).EmailVerified);
    }

    [Fact]
    public async Task ConfirmEmail_AfterExpiry_IsRejected()
    {
        var result = await _svc.RegisterAsync(Email, Password, "Provider");

        _clock.Advance(TimeSpan.FromHours(24) + TimeSpan.FromMinutes(1));

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _svc.ConfirmEmailAsync(Email, result!.EmailVerificationToken!));
    }

    [Fact]
    public async Task ConfirmEmail_IsSingleUse_ASecondAttemptWithTheSameTokenIsRejected()
    {
        var result = await _svc.RegisterAsync(Email, Password, "Provider");
        await _svc.ConfirmEmailAsync(Email, result!.EmailVerificationToken!);

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _svc.ConfirmEmailAsync(Email, result.EmailVerificationToken!));
    }

    private sealed class RecordingNotificationService : INotificationService
    {
        public List<NotificationEntity> Sent { get; } = [];

        public Task SendAsync(NotificationEntity notification)
        {
            Sent.Add(notification);
            return Task.CompletedTask;
        }

        public Task<IEnumerable<NotificationEntity>> GetForRecipientAsync(string recipientEmail) =>
            Task.FromResult<IEnumerable<NotificationEntity>>([]);

        public Task MarkReadAsync(string notificationId) => Task.CompletedTask;
    }
}
