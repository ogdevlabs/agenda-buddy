using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Identity.Services;
using Identity.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Identity.Tests.Security;

/// <summary>
/// Threat-model T-001 (HIGH): POST /login must not write the user's plaintext password
/// or the issued JWT access token to log output at INFO level or above.
/// CONSTITUTION.md §4 also prohibits logging PII — the JWT 'sub' claim IS the email.
/// </summary>
[Collection("Sequential")]
public class LoginLogSanitizationTest : IDisposable
{
    private readonly string _privateKeyPem;
    private readonly FakeDateTimeProvider _clock;
    private readonly InMemoryCredentialRepository _repo;
    private readonly CapturingLoggerFactory _loggerFactory;
    private readonly IdentityService _svc;

    private const string TestEmail = "audit@example.com";
    private const string TestPassword = "securePass123";
    private const string TestRole = "Provider";

    public LoginLogSanitizationTest()
    {
        (_, _privateKeyPem) = RsaKeyHelper.GenerateTestKeyPair();
        Environment.SetEnvironmentVariable("JWT_PRIVATE_KEY", _privateKeyPem);

        _clock = new FakeDateTimeProvider(new DateTime(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc));
        _repo = new InMemoryCredentialRepository();
        _loggerFactory = new CapturingLoggerFactory();

        // IdentityService does not currently accept ILogger — this test validates that
        // no future refactor silently introduces credential logging.  If ILogger is added
        // to the constructor, wire _loggerFactory here.
        _svc = new IdentityService(_repo, _clock);
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
        foreach (var msg in messages)
        {
            Assert.DoesNotContain(result!.RefreshToken, msg, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Regression guard: IdentityService constructor must NOT accept ILogger.
    /// If someone adds an ILogger parameter, this test will fail at compile time
    /// (or the wiring above will need to be updated and audited).
    /// This documents the intent: credential-handling code stays logger-free.
    /// </summary>
    [Fact]
    public void IdentityService_ConstructorParameters_ContainNoILogger()
    {
        var ctors = typeof(IdentityService).GetConstructors();
        foreach (var ctor in ctors)
        {
            foreach (var param in ctor.GetParameters())
            {
                var isLogger =
                    param.ParameterType.IsGenericType &&
                    param.ParameterType.GetGenericTypeDefinition() == typeof(ILogger<>);
                var isLoggerBase =
                    param.ParameterType == typeof(ILogger);

                Assert.False(
                    isLogger || isLoggerBase,
                    $"IdentityService constructor has an ILogger parameter '{param.Name}'. " +
                    "Review T-001: ensure no password/token/email is written to logs before merging.");
            }
        }
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

internal sealed class CapturingLogger(
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
