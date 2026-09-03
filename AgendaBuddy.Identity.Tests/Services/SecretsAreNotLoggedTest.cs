using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AgendaBuddy.Identity.Services;
using AgendaBuddy.Identity.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AgendaBuddy.Identity.Tests.Services;

/// <summary>
/// An email-confirmation token and a password-reset token are bearer credentials: whoever holds one can
/// confirm that address or change that password. They were once written to the log in plaintext as a
/// stand-in for the email provider that did not exist, which meant anyone who could read the logs of a
/// deployed environment could take over any account. These tests fail if either value returns to a log
/// sink.
/// </summary>
[Collection("Sequential")]
public class SecretsAreNotLoggedTest : IDisposable
{
    private readonly string _privateKeyPem;
    private readonly FakeDateTimeProvider _clock;
    private readonly InMemoryCredentialRepository _repo;
    private readonly CapturingLogger _logger;
    private readonly IdentityService _svc;

    public SecretsAreNotLoggedTest()
    {
        (_, _privateKeyPem) = RsaKeyHelper.GenerateTestKeyPair();
        Environment.SetEnvironmentVariable("JWT_PRIVATE_KEY", _privateKeyPem);
        _clock = new FakeDateTimeProvider(new DateTime(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc));
        _repo = new InMemoryCredentialRepository();
        _logger = new CapturingLogger();
        _svc = new IdentityService(_repo, _clock, logger: _logger);
    }

    public void Dispose() => Environment.SetEnvironmentVariable("JWT_PRIVATE_KEY", null);

    [Fact]
    public async Task Register_DoesNotLogTheEmailConfirmationToken()
    {
        var result = await _svc.RegisterAsync("logging@example.com", "password123", "Provider");

        var token = result!.EmailVerificationToken;
        Assert.False(string.IsNullOrWhiteSpace(token));

        // The event itself is still recorded — only the secret is gone.
        Assert.Contains(_logger.Messages, m => m.Contains("credential.email-confirmation-requested"));
        Assert.DoesNotContain(_logger.Messages, m => m.Contains(token!));
    }

    [Fact]
    public async Task RequestPasswordReset_DoesNotLogTheResetToken()
    {
        await _svc.RegisterAsync("resetlogging@example.com", "password123", "Customer");
        _logger.Messages.Clear();

        var token = await _svc.RequestPasswordResetAsync("resetlogging@example.com");
        Assert.False(string.IsNullOrWhiteSpace(token));

        Assert.Contains(_logger.Messages, m => m.Contains("credential.password-reset-requested"));
        Assert.DoesNotContain(_logger.Messages, m => m.Contains(token!));
    }

    /// <summary>Records the rendered message of every log entry so it can be asserted against.</summary>
    private sealed class CapturingLogger : ILogger<IdentityService>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }
}
