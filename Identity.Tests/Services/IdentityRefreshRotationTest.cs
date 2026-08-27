using System;
using System.Threading.Tasks;
using Identity.Configurations;
using Identity.Services;
using Identity.Tests.Helpers;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Xunit;

namespace Identity.Tests.Services;

/// <summary>
/// F-021 AC-1 … AC-4: rotating a refresh token must never be able to destroy the account.
/// </summary>
/// <remarks>
/// <para>
/// The defect these pin was a <b>delete-then-insert</b>: <c>RefreshAsync</c> called
/// <c>FindOneAndDeleteAsync</c> on the whole <c>CredentialEntity</c> and re-inserted it a few lines
/// later. Any fault in between lost the email, password hash, role and reset flag irrecoverably, with
/// no audit trail and no log line — and because the <c>catch … when (IsMongoDown(ex))</c> wrapped the
/// re-insert, <b>the destructive path was the handled path</b>: a transient database blip returned a
/// tidy 503 to a user whose account no longer existed. A mobile client refreshes hourly, so this was
/// not a rare path.
/// </para>
/// <para>
/// Twenty passing tests surrounded that code. What none of them could express was a fault
/// <i>between</i> the read and the write (<c>11-testing.md:65</c>), which is why
/// <see cref="InMemoryCredentialRepository.FaultBetweenMatchAndWrite"/> had to exist before AC-2 could
/// be written at all.
/// </para>
/// </remarks>
[Collection("Sequential")]
public class IdentityRefreshRotationTest : IDisposable
{
    private const string Email = "rotate@example.com";
    private const string Password = "password123";

    private readonly FakeDateTimeProvider _clock =
        new(new DateTime(2026, 8, 22, 12, 0, 0, DateTimeKind.Utc));

    private readonly InMemoryCredentialRepository _repo = new();
    private readonly IdentityService _svc;

    public IdentityRefreshRotationTest()
    {
        var (_, privateKeyPem) = RsaKeyHelper.GenerateTestKeyPair();
        Environment.SetEnvironmentVariable("JWT_PRIVATE_KEY", privateKeyPem);

        _svc = new IdentityService(
            _repo,
            _clock,
            Options.Create(new LockoutOptions { MaxFailedAttempts = 3, WindowMinutes = 15 }));
    }

    public void Dispose() => Environment.SetEnvironmentVariable("JWT_PRIVATE_KEY", null);

    [Fact]
    public async Task Rotation_ChangesOnlyTheRefreshToken()
    {
        // AC-1.
        var registered = await _svc.RegisterAsync(Email, Password, "Provider");
        var before = await Stored();
        var (id, hash, role, mustReset, oldTokenHash) =
            (before.Id, before.PasswordHash, before.Role, before.MustResetPassword, before.RefreshToken!.Hash);

        await _svc.RefreshAsync(registered!.RefreshToken);

        var after = await Stored();
        Assert.Equal(id, after.Id);
        Assert.Equal(Email, after.Email);
        Assert.Equal(hash, after.PasswordHash);
        Assert.Equal(role, after.Role);
        Assert.Equal(mustReset, after.MustResetPassword);
        Assert.NotEqual(oldTokenHash, after.RefreshToken!.Hash);
    }

    [Fact]
    public async Task Rotation_WhenTheWriteFaults_LeavesTheCredentialIntact()
    {
        // AC-2 — the criterion the old design made unexpressible. A MongoException is used because
        // that is what IsMongoDown catches, so this reproduces the *handled* path: the caller sees a
        // 503, and the question is whether the account is still there afterwards.
        var registered = await _svc.RegisterAsync(Email, Password, "Provider");
        var before = await Stored();
        var (hash, role, tokenHash) = (before.PasswordHash, before.Role, before.RefreshToken!.Hash);

        _repo.FaultBetweenMatchAndWrite = () => throw new MongoException("injected mid-rotation fault");

        await Assert.ThrowsAsync<ServiceUnavailableException>(
            () => _svc.RefreshAsync(registered!.RefreshToken));

        _repo.FaultBetweenMatchAndWrite = null;

        var after = await Stored();
        Assert.Equal(Email, after.Email);
        Assert.Equal(hash, after.PasswordHash);
        Assert.Equal(role, after.Role);

        // And the old token still works, so the client's retry succeeds rather than stranding a user
        // whose account survived but whose session did not.
        Assert.Equal(tokenHash, after.RefreshToken!.Hash);
        Assert.NotNull(await _svc.RefreshAsync(registered!.RefreshToken));
    }

    [Fact]
    public async Task Rotation_IsSingleUse_SoAReplayedTokenIssuesNothing()
    {
        // AC-3. Single use is preserved by the old hash being part of the update *filter*, not by a
        // prior delete: the update matches only while the old hash is still stored.
        var registered = await _svc.RegisterAsync(Email, Password, "Provider");
        var first = await _svc.RefreshAsync(registered!.RefreshToken);

        await Assert.ThrowsAsync<UnauthorizedException>(() => _svc.RefreshAsync(registered.RefreshToken));

        var after = await Stored();
        Assert.Equal(IdentityService.HashToken(first!.RefreshToken), after.RefreshToken!.Hash);
    }

    [Fact]
    public async Task T104_Rotation_OnALockedAccount_IsRefused()
    {
        // Threat T-104 / AC-4: locking stops new passwords being tried, but an attacker already
        // holding a refresh token would keep minting access tokens for the 24 hours it lives unless
        // the lock is part of the rotation filter.
        var registered = await _svc.RegisterAsync(Email, Password, "Provider");
        await Lock();

        await Assert.ThrowsAsync<UnauthorizedException>(() => _svc.RefreshAsync(registered!.RefreshToken));

        // Unchanged: refusing must not consume the token either, or the lock would double as a
        // session killer for the legitimate owner.
        var after = await Stored();
        Assert.Equal(IdentityService.HashToken(registered!.RefreshToken), after.RefreshToken!.Hash);
    }

    [Fact]
    public async Task T104_Rotation_ResumesOnceTheLockExpires()
    {
        var registered = await _svc.RegisterAsync(Email, Password, "Provider");
        await Lock();

        _clock.Advance(TimeSpan.FromMinutes(16));

        Assert.NotNull(await _svc.RefreshAsync(registered!.RefreshToken));
    }

    [Fact]
    public async Task Rotation_NeverReplacesTheWholeDocument()
    {
        // AC-11's rotation half. A whole-document replacement is what the defect was; asserting the
        // *shape* of the write is what stops it coming back as a "simpler" refactor.
        var registered = await _svc.RegisterAsync(Email, Password, "Provider");
        await _svc.RefreshAsync(registered!.RefreshToken);

        Assert.Equal(0, _repo.WholeDocumentReplacements);
        Assert.All(_repo.AppliedUpdates, update =>
            Assert.All(update.Names, name => Assert.StartsWith("$", name)));
    }

    private async Task Lock()
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            await Assert.ThrowsAsync<UnauthorizedException>(() => _svc.LoginAsync(Email, "wrong-password"));
        }

        Assert.NotNull((await Stored()).LockUntil);
    }

    private async Task<AgendaBuddy.Library.Entities.CredentialEntity> Stored() =>
        Assert.Single(await _repo.GetAllAsync());
}
