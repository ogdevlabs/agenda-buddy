using System;
using System.Threading.Tasks;
using AgendaBuddy.Identity.Services;
using AgendaBuddy.Identity.Tests.Helpers;
using AgendaBuddy.Library.Entities;
using AgendaBuddy.Library.Services;
using Xunit;

namespace AgendaBuddy.Identity.Tests.Services;

/// <summary>
/// Registration issues a single-use email-confirmation token so an account holder can prove ownership
/// before Identity creates an authenticated session.
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
        Assert.Matches("^[0-9]{6}$", result.EmailVerificationCode);
        var stored = await Stored();
        Assert.False(stored.EmailVerified);
        Assert.NotNull(stored.EmailVerificationToken);
        Assert.Equal(IdentityService.HashToken(result.EmailVerificationToken!), stored.EmailVerificationToken!.Hash);
        Assert.Equal(IdentityService.HashToken(result.EmailVerificationCode), stored.EmailVerificationToken.CodeHash);
        Assert.Equal(_clock.UtcNow.AddMinutes(15), stored.EmailVerificationToken.CodeExpiry);
        Assert.Equal(_clock.UtcNow.AddHours(24), stored.EmailVerificationToken.Expiry);
        // The raw token is never the hash — the whole point of storing only the hash.
        Assert.NotEqual(result.EmailVerificationToken, stored.EmailVerificationToken.Hash);
        Assert.NotEqual(result.EmailVerificationCode, stored.EmailVerificationToken.CodeHash);
    }

    [Fact]
    public async Task ConfirmEmailCode_WithMatchingEmailAndCode_VerifiesTheAccount()
    {
        var result = await _svc.RegisterAsync(Email, Password, "Provider");

        await _svc.ConfirmEmailCodeAsync(Email, result.EmailVerificationCode);

        var stored = await Stored();
        Assert.True(stored.EmailVerified);
        Assert.Null(stored.EmailVerificationToken);
    }

    [Fact]
    public async Task ConfirmEmailCode_WithAnotherEmail_IsRejected()
    {
        var result = await _svc.RegisterAsync(Email, Password, "Provider");

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _svc.ConfirmEmailCodeAsync("someone-else@example.com", result.EmailVerificationCode));

        Assert.False((await Stored()).EmailVerified);
    }

    [Fact]
    public async Task ConfirmEmailCode_AfterExpiry_IsRejected()
    {
        var result = await _svc.RegisterAsync(Email, Password, "Provider");
        _clock.Advance(TimeSpan.FromMinutes(16));

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _svc.ConfirmEmailCodeAsync(Email, result.EmailVerificationCode));
    }

    [Fact]
    public async Task ConfirmEmailCode_IsSingleUse()
    {
        var result = await _svc.RegisterAsync(Email, Password, "Provider");
        await _svc.ConfirmEmailCodeAsync(Email, result.EmailVerificationCode);

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _svc.ConfirmEmailCodeAsync(Email, result.EmailVerificationCode));
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
    public async Task Register_SendsBilingualEmailWithAppButtonsAndSixDigitCode()
    {
        var sender = new RecordingEmailSender();
        var svc = new IdentityService(_repo, _clock, emailSender: sender);

        var result = await svc.RegisterAsync(Email, Password, "Provider");

        Assert.Equal("Confirm your email / Confirma tu correo", sender.Subject);
        Assert.Contains("Welcome to AgendaMe", sender.Text);
        Assert.Contains("Bienvenido a AgendaMe", sender.Text);
        Assert.Contains("Confirm email", sender.Html);
        Assert.Contains("Confirmar correo", sender.Html);
        var link = $"agendame://email/confirm-email?token={Uri.EscapeDataString(result.EmailVerificationToken)}";
        Assert.Equal(2, sender.Html.Split(link, StringSplitOptions.None).Length - 1);
        Assert.Equal(2, sender.Html.Split(result.EmailVerificationCode, StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("agendame.app", sender.Html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Email, sender.Html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConfirmEmail_WithAValidToken_SetsEmailVerifiedAndClearsTheToken()
    {
        var result = await _svc.RegisterAsync(Email, Password, "Provider");

        await _svc.ConfirmEmailAsync(result.EmailVerificationToken);

        var stored = await Stored();
        Assert.True(stored.EmailVerified);
        Assert.Null(stored.EmailVerificationToken);
    }

    [Fact]
    public async Task Login_IsBlockedUntilEmailIsConfirmed()
    {
        var result = await _svc.RegisterAsync(Email, Password, "Provider");

        await Assert.ThrowsAsync<EmailVerificationRequiredException>(
            () => _svc.LoginAsync(Email, Password));

        await _svc.ConfirmEmailAsync(result.EmailVerificationToken);

        Assert.NotNull(await _svc.LoginAsync(Email, Password));
    }

    [Fact]
    public async Task ConfirmEmail_WithAWrongToken_IsRejectedAndChangesNothing()
    {
        await _svc.RegisterAsync(Email, Password, "Provider");

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _svc.ConfirmEmailAsync("not-the-real-token"));

        Assert.False((await Stored()).EmailVerified);
    }

    [Fact]
    public async Task ConfirmEmail_AfterExpiry_IsRejected()
    {
        var result = await _svc.RegisterAsync(Email, Password, "Provider");

        _clock.Advance(TimeSpan.FromHours(24) + TimeSpan.FromMinutes(1));

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _svc.ConfirmEmailAsync(result.EmailVerificationToken));
    }

    [Fact]
    public async Task ConfirmEmail_IsSingleUse_ASecondAttemptWithTheSameTokenIsRejected()
    {
        var result = await _svc.RegisterAsync(Email, Password, "Provider");
        await _svc.ConfirmEmailAsync(result.EmailVerificationToken);

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _svc.ConfirmEmailAsync(result.EmailVerificationToken));
    }

    [Fact]
    public async Task Resend_RotatesTheTokenAndInvalidatesTheOriginalLink()
    {
        var sender = new RecordingEmailSender();
        var svc = new IdentityService(_repo, _clock, emailSender: sender);
        var registration = await svc.RegisterAsync(Email, Password, "Provider");

        await svc.RequestEmailVerificationAsync(Email);

        Assert.NotNull(sender.Token);
        Assert.NotNull(sender.Code);
        Assert.NotEqual(registration.EmailVerificationToken, sender.Token);
        Assert.NotEqual(registration.EmailVerificationCode, sender.Code);
        await Assert.ThrowsAsync<UnauthorizedException>(
            () => svc.ConfirmEmailAsync(registration.EmailVerificationToken));
        await Assert.ThrowsAsync<UnauthorizedException>(
            () => svc.ConfirmEmailCodeAsync(Email, registration.EmailVerificationCode));
        await svc.ConfirmEmailCodeAsync(Email, sender.Code!);
    }

    [Fact]
    public async Task Resend_ForUnknownAddressReturnsWithoutSending()
    {
        var sender = new RecordingEmailSender();
        var svc = new IdentityService(_repo, _clock, emailSender: sender);

        await svc.RequestEmailVerificationAsync("unknown@example.com");

        Assert.Equal(0, sender.SendCount);
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

    private sealed class RecordingEmailSender : IEmailSender
    {
        public string Subject { get; private set; } = string.Empty;
        public string Text { get; private set; } = string.Empty;
        public string Html { get; private set; } = string.Empty;
        public string? Token { get; private set; }
        public string? Code { get; private set; }
        public int SendCount { get; private set; }

        public Task<bool> SendAsync(
            string toAddress,
            string subject,
            string body,
            CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<bool> SendAsync(
            string toAddress,
            string subject,
            string body,
            string htmlBody,
            CancellationToken cancellationToken = default)
        {
            SendCount++;
            Subject = subject;
            Text = body;
            Html = htmlBody;
            var marker = "confirm-email?token=";
            var start = body.IndexOf(marker, StringComparison.Ordinal);
            if (start >= 0)
            {
                start += marker.Length;
                var end = body.IndexOfAny(['\r', '\n'], start);
                Token = Uri.UnescapeDataString(end < 0 ? body[start..] : body[start..end]);
            }
            Code = System.Text.RegularExpressions.Regex.Match(body, @"\b\d{6}\b").Value;
            return Task.FromResult(true);
        }
    }
}
