using System;
using System.Threading.Tasks;
using Identity.Configurations;
using Identity.Services;
using Identity.Tests.Helpers;
using AgendaBuddy.Library.Entities;
using Microsoft.Extensions.Options;
using Xunit;

namespace Identity.Tests.Services;

/// <summary>
/// F-021 AC-7 … AC-11: the per-account half of login defence — a counter that is never
/// read-modify-written, and a lock that expires by itself.
/// </summary>
/// <remarks>
/// <para>
/// The per-account counter and the per-IP limiter are <b>not redundant</b>. Identity verifies an unknown
/// email against a dummy hash to keep enumeration constant-time (threat T-005,
/// <c>IdentityService.cs:96</c>), so an attacker using random addresses spends the same 262 ms of server
/// CPU per request while generating <b>no per-account state at all</b>. Only the limiter sees that
/// traffic; only the counter sees a targeted attack on one known account. Both, or neither works.
/// </para>
/// <para>
/// Threshold and window are set small here on purpose. The shipped defaults are 10 attempts / 15
/// minutes, derived from the measured BCrypt cost (262 ms per verify at work factor 12 on this
/// hardware, so ≈3.8 attempts/sec/core); a test that used them would spend 2.6 s of BCrypt to prove
/// arithmetic.
/// </para>
/// </remarks>
[Collection("Sequential")]
public class IdentityLockoutTest : IDisposable
{
    private const string Email = "lockme@example.com";
    private const string Password = "password123";
    private const int Threshold = 3;

    private readonly FakeDateTimeProvider _clock =
        new(new DateTime(2026, 8, 22, 12, 0, 0, DateTimeKind.Utc));

    private readonly InMemoryCredentialRepository _repo = new();
    private readonly IdentityService _svc;

    public IdentityLockoutTest()
    {
        var (_, privateKeyPem) = RsaKeyHelper.GenerateTestKeyPair();
        Environment.SetEnvironmentVariable("JWT_PRIVATE_KEY", privateKeyPem);

        _svc = new IdentityService(
            _repo,
            _clock,
            Options.Create(new LockoutOptions { MaxFailedAttempts = Threshold, WindowMinutes = 15 }));
    }

    public void Dispose() => Environment.SetEnvironmentVariable("JWT_PRIVATE_KEY", null);

    [Fact]
    public async Task T102_AFailedLogin_IncrementsTheCounterAtomically_NeverReplacingTheDocument()
    {
        // AC-11 / threat T-102. The counter turns a read path into an attacker-influenced write path on
        // the one collection with no backups, so the write must be the narrowest possible thing.
        await _svc.RegisterAsync(Email, Password, "Provider");
        _repo.AppliedUpdates.Clear();

        await Assert.ThrowsAsync<UnauthorizedException>(() => _svc.LoginAsync(Email, "wrong-password"));

        var update = Assert.Single(_repo.AppliedUpdates);
        Assert.Equal(1, update["$inc"]["failed_attempts"].ToInt32());
        Assert.Equal(0, _repo.WholeDocumentReplacements);
        Assert.Equal(1, (await Stored()).FailedAttempts);
    }

    [Fact]
    public async Task T102_AFailedLoginForAnUnknownEmail_CreatesNoDocument()
    {
        // AC-9. Never upserting is a property of the repository primitive, not of this call site — but
        // it is asserted from here because this is the path an attacker drives.
        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _svc.LoginAsync("nobody@example.com", Password));

        Assert.Empty(await _repo.GetAllAsync());
    }

    [Fact]
    public async Task AfterTheThreshold_TheAccountIsLocked_AndTheRefusalLooksIdenticalToAWrongPassword()
    {
        // AC-7. A distinct status or message for "locked" would tell an attacker which addresses exist
        // and which they have successfully locked, undoing the enumeration mitigation T-005 added.
        await _svc.RegisterAsync(Email, Password, "Provider");

        var wrongPassword = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _svc.LoginAsync(Email, "wrong-password"));

        for (var attempt = 1; attempt < Threshold; attempt++)
        {
            await Assert.ThrowsAsync<UnauthorizedException>(() => _svc.LoginAsync(Email, "wrong-password"));
        }

        Assert.NotNull((await Stored()).LockUntil);

        // The correct password is now refused too, and refused the same way.
        var locked = await Assert.ThrowsAsync<UnauthorizedException>(() => _svc.LoginAsync(Email, Password));
        Assert.Equal(wrongPassword.Message, locked.Message);
    }

    [Fact]
    public async Task ALockedAccount_SpendsNoBcryptAndTakesNoFurtherWrite()
    {
        // Threat T-101's other half, and design decision D-9: the lock is checked *before* the verify,
        // or a locked account costs 262 ms of CPU per attempt and the lock amplifies the very denial of
        // service it sits beside. Asserted through the counter: the increment only happens on the
        // verify-failed path, so a locked attempt leaving the counter untouched proves the short
        // circuit fired first.
        await _svc.RegisterAsync(Email, Password, "Provider");
        await FailUntilLocked();

        var attemptsWhenLocked = (await Stored()).FailedAttempts;
        _repo.AppliedUpdates.Clear();

        await Assert.ThrowsAsync<UnauthorizedException>(() => _svc.LoginAsync(Email, "wrong-password"));

        Assert.Empty(_repo.AppliedUpdates);
        Assert.Equal(attemptsWhenLocked, (await Stored()).FailedAttempts);
    }

    [Fact]
    public async Task WhenTheWindowElapses_TheCorrectPasswordSucceeds_WithNoUnlockWrite()
    {
        // AC-8. F-022 does not exist, so a lock that needed clearing would leave a real provider with
        // no way back into their own business — and would let an attacker strand one deliberately.
        // "Unlocked" is therefore the absence of a future value, which costs no write and needs no job.
        await _svc.RegisterAsync(Email, Password, "Provider");
        await FailUntilLocked();

        var writesWhileLocked = _repo.AppliedUpdates.Count;
        _clock.Advance(TimeSpan.FromMinutes(16));

        var result = await _svc.LoginAsync(Email, Password);

        Assert.NotNull(result);
        // Exactly one further write: the successful login's own rotation-and-reset. Nothing cleared the
        // lock, because nothing had to.
        Assert.Equal(writesWhileLocked + 1, _repo.AppliedUpdates.Count);
    }

    [Fact]
    public async Task ASuccessfulLogin_ResetsTheCounterAndClearsAnyLock()
    {
        // AC-10, and it is one write rather than two: the refresh-token rotation login already
        // performed now carries the reset, so the success path adds no extra round trip.
        await _svc.RegisterAsync(Email, Password, "Provider");
        await Assert.ThrowsAsync<UnauthorizedException>(() => _svc.LoginAsync(Email, "wrong-password"));
        Assert.Equal(1, (await Stored()).FailedAttempts);

        _repo.AppliedUpdates.Clear();
        Assert.NotNull(await _svc.LoginAsync(Email, Password));

        var stored = await Stored();
        Assert.Equal(0, stored.FailedAttempts);
        Assert.Null(stored.LockUntil);
        Assert.Single(_repo.AppliedUpdates);
        Assert.Equal(0, _repo.WholeDocumentReplacements);
    }

    [Fact]
    public async Task ALockUntilInThePast_ReadsAsUnlockedWithoutAWrite()
    {
        // AC-8's read half, stated separately because it is the property that removes the need for a
        // sweeper: a stale lock_until is indistinguishable from no lock to every reader.
        await _svc.RegisterAsync(Email, Password, "Provider");
        var stored = await Stored();
        stored.LockUntil = _clock.UtcNow.AddMinutes(-1);
        _repo.AppliedUpdates.Clear();

        Assert.NotNull(await _svc.LoginAsync(Email, Password));
    }

    private async Task FailUntilLocked()
    {
        for (var attempt = 0; attempt < Threshold; attempt++)
        {
            await Assert.ThrowsAsync<UnauthorizedException>(() => _svc.LoginAsync(Email, "wrong-password"));
        }

        Assert.NotNull((await Stored()).LockUntil);
    }

    private async Task<CredentialEntity> Stored() => Assert.Single(await _repo.GetAllAsync());
}
