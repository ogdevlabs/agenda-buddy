using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AgendaBuddy.Identity.Configurations;
using AgendaBuddy.Identity.Services;
using AgendaBuddy.Identity.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace AgendaBuddy.Identity.Tests.Security;

/// <summary>
/// Two threats pull in opposite directions and are both
/// satisfied here: credential mutations <b>are</b> logged, and no log line carries a password, a token,
/// or an email address.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>This file replaces a test that asserted the opposite.</b> Previously,
/// <c>IdentityService_ConstructorParameters_ContainNoILogger</c> asserted by reflection that
/// <c>IdentityService</c> had <b>no</b> logger at all — a structural proxy for "no credential material
/// in logs", from a time when nothing in Identity logged anything. Credential mutations must now be
/// logged (the account-destroying refresh was silent <i>and</i> untraceable,
/// which is what made it survivable), so the proxy had to go. It is replaced by the stronger assertion
/// the proxy was standing in for: the logger exists, and its output is inspected for every sensitive
/// value. This is a deliberate deviation from "delete no pre-existing test", the same
/// class of decision as the ADR-025 deletion.
/// </para>
/// <para>
/// <b>The three sanitization tests below were vacuous before this change.</b> They iterated
/// <c>GetMessages(Information)</c> on a logger factory that was never wired to anything, so they
/// asserted over an empty list and could not have failed. They now run against real log output.
/// </para>
/// <para>
/// PII is redacted by <b>hashing, not truncation</b> (design decision D-8):
/// <c>PiiRedactingProcessor</c> protects spans, not logs, so nothing downstream would catch an address
/// written here — and this project's own telemetry rollout is precedent for exactly that
/// (real customer emails exported in <c>url.path</c>).
/// </para>
/// </remarks>
[Collection("Sequential")]
public class LoginLogSanitizationTest : IDisposable
{
    private readonly FakeDateTimeProvider _clock;
    private readonly InMemoryCredentialRepository _repo;
    private readonly CapturingLoggerFactory _loggerFactory;
    private readonly IdentityService _svc;

    private const string TestEmail = "audit@example.com";
    private const string TestPassword = "securePass123";
    private const string TestRole = "Provider";

    public LoginLogSanitizationTest()
    {
        var (_, privateKeyPem) = RsaKeyHelper.GenerateTestKeyPair();
        Environment.SetEnvironmentVariable("JWT_PRIVATE_KEY", privateKeyPem);

        _clock = new FakeDateTimeProvider(new DateTime(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc));
        _repo = new InMemoryCredentialRepository();
        _loggerFactory = new CapturingLoggerFactory();

        _svc = new IdentityService(
            _repo,
            _clock,
            Options.Create(new LockoutOptions { MaxFailedAttempts = 2, WindowMinutes = 15 }),
            _loggerFactory.CreateLogger<IdentityService>());
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("JWT_PRIVATE_KEY", null);
    }

    [Fact]
    public async Task Login_ValidCredentials_DoesNotLogPassword()
    {
        await _svc.RegisterAsync(TestEmail, TestPassword, TestRole);
        var result = await _svc.LoginAsync(TestEmail, TestPassword);

        Assert.NotNull(result);

        var messages = _loggerFactory.GetMessages(LogLevel.Information);
        Assert.NotEmpty(messages);
        foreach (var msg in messages)
        {
            Assert.DoesNotContain(TestPassword, msg, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Login_ValidCredentials_DoesNotLogAccessToken()
    {
        await _svc.RegisterAsync(TestEmail, TestPassword, TestRole);
        var result = await _svc.LoginAsync(TestEmail, TestPassword);

        Assert.NotNull(result);

        var messages = _loggerFactory.GetMessages(LogLevel.Information);
        Assert.NotEmpty(messages);
        foreach (var msg in messages)
        {
            Assert.DoesNotContain(result!.AccessToken, msg, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Login_ValidCredentials_DoesNotLogRefreshToken()
    {
        await _svc.RegisterAsync(TestEmail, TestPassword, TestRole);
        var result = await _svc.LoginAsync(TestEmail, TestPassword);

        Assert.NotNull(result);

        var messages = _loggerFactory.GetMessages(LogLevel.Information);
        Assert.NotEmpty(messages);
        foreach (var msg in messages)
        {
            Assert.DoesNotContain(result!.RefreshToken, msg, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task T105_EveryCredentialMutation_IsLoggedWithItsOperationAndOutcome()
    {
        // AC-16, first half. The refresh defect destroyed accounts with no audit event and no log
        // line, so an account lost that way left no trace of ever having existed. Identity does not use
        // the EventStore (putting credential-shaped documents into the
        // collection every other service writes to is its own problem), so logs are the record.
        var registered = await _svc.RegisterAsync(TestEmail, TestPassword, TestRole);
        var rotated = await _svc.RefreshAsync(registered!.RefreshToken);

        // Two wrong passwords reach the threshold this test configured, so the lock is applied.
        await Assert.ThrowsAsync<UnauthorizedException>(() => _svc.LoginAsync(TestEmail, "wrong"));
        await Assert.ThrowsAsync<UnauthorizedException>(() => _svc.LoginAsync(TestEmail, "wrong"));

        // Waiting the lock out and signing in clears the counter — the "reset" mutation. Note that this
        // login rotates the refresh token again, so logging out has to use the token it returned: the one
        // from the earlier rotation is no longer the stored hash, which is single-use working correctly.
        _clock.Advance(TimeSpan.FromMinutes(16));
        var signedIn = await _svc.LoginAsync(TestEmail, TestPassword);
        Assert.NotNull(rotated);
        await _svc.LogoutAsync(signedIn!.RefreshToken);

        var log = string.Join(Environment.NewLine, _loggerFactory.GetMessages(LogLevel.Information));

        Assert.Contains("credential.created", log, StringComparison.Ordinal);
        Assert.Contains("credential.rotated", log, StringComparison.Ordinal);
        Assert.Contains("credential.login-failed", log, StringComparison.Ordinal);
        Assert.Contains("credential.locked", log, StringComparison.Ordinal);
        Assert.Contains("credential.reset", log, StringComparison.Ordinal);
        Assert.Contains("credential.session-ended", log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task T105_NoLogLine_ContainsAnEmailAddress()
    {
        // AC-16, second half, and the reason D-8 chose a hash prefix over anything truncated:
        // "aud…@example.com" is still an identifier for a cluster of this size.
        var registered = await _svc.RegisterAsync(TestEmail, TestPassword, TestRole);
        await _svc.RefreshAsync(registered!.RefreshToken);
        await Assert.ThrowsAsync<UnauthorizedException>(() => _svc.LoginAsync(TestEmail, "wrong"));
        await Assert.ThrowsAsync<UnauthorizedException>(() => _svc.LoginAsync(TestEmail, "wrong"));
        await Assert.ThrowsAsync<UnauthorizedException>(() => _svc.LoginAsync("stranger@example.com", "wrong"));

        var messages = _loggerFactory.GetMessages(LogLevel.Information);
        Assert.NotEmpty(messages);

        foreach (var msg in messages)
        {
            Assert.DoesNotContain(TestEmail, msg, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("audit", msg, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("stranger", msg, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("@", msg, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void TheAccountReference_IsNotReversibleToAnAddress()
    {
        // A hash prefix is a correlation handle, not a pseudonym that survives a dictionary attack over
        // a small user base — but it is one-way, unlike anything derived from the address itself.
        var reference = IdentityService.AccountReference(TestEmail);

        Assert.DoesNotContain("audit", reference, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@", reference, StringComparison.Ordinal);
        Assert.Equal(reference, IdentityService.AccountReference(TestEmail.ToUpperInvariant()));
        Assert.NotEqual(reference, IdentityService.AccountReference("other@example.com"));
    }
}

/// <summary>
/// Minimal ILoggerFactory that records log messages at INFO level and above.
/// Used to assert that no sensitive values appear in log output.
/// </summary>
public sealed class CapturingLoggerFactory : ILoggerFactory
{
    private readonly List<(LogLevel Level, string Message)> _entries = new();

    public ILogger CreateLogger(string categoryName) =>
        new CapturingLogger(categoryName, _entries);

    public ILogger<T> CreateLogger<T>() => new CapturingLogger<T>(_entries);

    public void AddProvider(ILoggerProvider provider) { }

    public void Dispose() { }

    /// <summary>Returns all formatted log messages at or above the specified level.</summary>
    public IReadOnlyList<string> GetMessages(LogLevel minimumLevel)
    {
        var result = new List<string>();
        foreach (var (level, message) in _entries)
        {
            if (level >= minimumLevel)
                result.Add(message);
        }
        return result;
    }
}

internal class CapturingLogger(
    string categoryName,
    List<(LogLevel Level, string Message)> entries) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;
        var message = formatter(state, exception);
        entries.Add((logLevel, $"[{categoryName}] {message}"));
    }
}

internal sealed class CapturingLogger<T>(List<(LogLevel Level, string Message)> entries)
    : CapturingLogger(typeof(T).FullName ?? typeof(T).Name, entries), ILogger<T>;
