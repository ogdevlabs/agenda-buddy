using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using AgendaBuddy.Identity.Configurations;
using AgendaBuddy.Identity.Requests;
using AgendaBuddy.Library.Entities;
using AgendaBuddy.Library.Extensions;
using AgendaBuddy.Library.Repositories;
using AgendaBuddy.Library.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AgendaBuddy.Identity.Services;

public class IdentityService(
    IRepository<CredentialEntity> repository,
    IDateTimeProvider clock,
    IOptions<LockoutOptions>? lockoutOptions = null,
    ILogger<IdentityService>? logger = null,
    INotificationService? notificationService = null,
    ITokenRevocationStore? tokenRevocationStore = null,
    IEmailSender? emailSender = null,
    IOptions<EmailOptions>? emailOptions = null,
    IDeviceTokenService? deviceTokenService = null)
{
    private const string PrivateKeyEnvVar = "JWT_PRIVATE_KEY";
    private const string Issuer = "agenda-buddy-identity";
    private static readonly string[] AllowedRoles = ["Provider", "Customer"];
    private static readonly TimeSpan ResetTokenLifetime = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan EmailVerificationTokenLifetime = TimeSpan.FromHours(24);
    private static readonly TimeSpan EmailVerificationCodeLifetime = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Lockout thresholds. Both parameters are optional so the shipped defaults apply to any caller that
    /// has not configured them — including the 20-odd unit tests that predate this feature.
    /// </summary>
    private readonly LockoutOptions _lockout = lockoutOptions?.Value ?? new LockoutOptions();

    private readonly ILogger _log = logger ?? NullLogger<IdentityService>.Instance;

    private readonly EmailOptions _email = emailOptions?.Value ?? new EmailOptions();

    private static readonly string DummyHash =
        BCrypt.Net.BCrypt.HashPassword(Guid.Empty.ToString(), workFactor: 12);

    public async Task<RegistrationResponse> RegisterAsync(string email, string password, string role)
    {
        email = email.ToLowerInvariant();

        if (password.Length < 8 || string.IsNullOrWhiteSpace(password))
            throw new AuthValidationException("Password must be at least 8 characters.");

        if (!AllowedRoles.Contains(role))
            throw new AuthValidationException("Role must be 'Provider' or 'Customer'.");

        var filter = new BsonDocument("email", email);

        CredentialEntity? existing;
        try
        {
            existing = await repository.FindOneAsync(filter);
        }
        catch (Exception ex) when (IsMongoDown(ex))
        {
            throw new ServiceUnavailableException();
        }

        if (existing is not null)
            throw new ConflictException("An account with this email already exists.");

        var hash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
        var (verificationOpaque, verificationHash) = CreateRefreshToken();
        var verificationCode = CreateEmailVerificationCode();
        var verificationExpiry = clock.UtcNow.Add(EmailVerificationTokenLifetime);

        var credential = new CredentialEntity
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Email = email,
            PasswordHash = hash,
            Role = role,
            MustResetPassword = false,
            EmailVerified = false,
            EmailVerificationToken = new EmailVerificationTokenDocument
            {
                Hash = verificationHash,
                CodeHash = HashToken(verificationCode),
                CodeExpiry = clock.UtcNow.Add(EmailVerificationCodeLifetime),
                Expiry = verificationExpiry
            }
        };

        try
        {
            await repository.InsertAsync(credential);
        }
        catch (Exception ex) when (IsMongoDown(ex))
        {
            throw new ServiceUnavailableException();
        }

        _log.LogInformation(
            "credential.created ok for {Account} as {Role}", AccountReference(email), role);

        // The token itself is never logged. It is a bearer credential for the confirmation route, so a
        // log sink is a place it can be read by anyone with log access and replayed.
        _log.LogInformation(
            "credential.email-confirmation-requested for {Account}: expires {Expiry:O}",
            AccountReference(email), verificationExpiry);

        if (notificationService is not null)
        {
            await notificationService.SendAsync(new NotificationEntity(
                recipientEmail: email,
                subject: "Confirm your email address",
                body: "Welcome to AgendaMe. Please confirm you own this email address to finish setting up your account.",
                type: NotificationType.EmailConfirmationRequested,
                appointmentIdentifier: string.Empty));
        }

        await SendConfirmationEmailAsync(email, verificationOpaque, verificationCode);

        return new RegistrationResponse(verificationOpaque, verificationCode);
    }

    /// <summary>
    /// Rotates the confirmation token for an unverified account. Unknown and already-verified addresses
    /// return identically so this unauthenticated recovery path cannot enumerate accounts.
    /// </summary>
    public async Task RequestEmailVerificationAsync(string email)
    {
        email = email.ToLowerInvariant();
        var (verificationOpaque, verificationHash) = CreateRefreshToken();
        var verificationCode = CreateEmailVerificationCode();
        var verificationExpiry = clock.UtcNow.Add(EmailVerificationTokenLifetime);

        CredentialEntity? credential;
        try
        {
            credential = await repository.FindOneAndUpdateAsync(
                new BsonDocument
                {
                    { "email", email },
                    { "email_verified", false }
                },
                new BsonDocument(
                    "$set",
                    new BsonDocument(
                        "email_verification_token",
                        new BsonDocument
                        {
                            { "hash", verificationHash },
                            { "code_hash", HashToken(verificationCode) },
                            { "code_expiry", clock.UtcNow.Add(EmailVerificationCodeLifetime) },
                            { "expiry", verificationExpiry }
                        })));
        }
        catch (Exception ex) when (IsMongoDown(ex))
        {
            throw new ServiceUnavailableException();
        }

        if (credential is null) return;

        _log.LogInformation(
            "credential.email-confirmation-requested for {Account}: expires {Expiry:O}",
            AccountReference(email), verificationExpiry);
        await SendConfirmationEmailAsync(email, verificationOpaque, verificationCode);
    }

    /// <summary>
    /// Consumes a single-use email-confirmation token before Identity will issue a session.
    /// </summary>
    public async Task ConfirmEmailAsync(string token)
    {
        var presentedHash = HashToken(token);
        var now = clock.UtcNow;

        CredentialEntity? credential;
        try
        {
            var filter = new BsonDocument
            {
                { "email_verification_token.hash", presentedHash },
                { "email_verification_token.expiry", new BsonDocument("$gt", now) }
            };

            credential = await repository.FindOneAndUpdateAsync(
                filter,
                new BsonDocument
                {
                    { "$set", new BsonDocument("email_verified", true) },
                    { "$unset", new BsonDocument("email_verification_token", 1) }
                });
        }
        catch (Exception ex) when (IsMongoDown(ex))
        {
            throw new ServiceUnavailableException();
        }

        if (credential is null)
            throw new UnauthorizedException("This confirmation link is invalid or has expired.");

        _log.LogInformation(
            "credential.email-confirmed ok for {Account}", AccountReference(credential.Email));
    }

    public async Task ConfirmEmailCodeAsync(string email, string code)
    {
        email = email.ToLowerInvariant();
        var now = clock.UtcNow;

        CredentialEntity? credential;
        try
        {
            credential = await repository.FindOneAndUpdateAsync(
                new BsonDocument
                {
                    { "email", email },
                    { "email_verification_token.code_hash", HashToken(code) },
                    { "email_verification_token.code_expiry", new BsonDocument("$gt", now) }
                },
                new BsonDocument
                {
                    { "$set", new BsonDocument("email_verified", true) },
                    { "$unset", new BsonDocument("email_verification_token", 1) }
                });
        }
        catch (Exception ex) when (IsMongoDown(ex))
        {
            throw new ServiceUnavailableException();
        }

        if (credential is null)
            throw new UnauthorizedException("This confirmation code is invalid or has expired.");

        _log.LogInformation(
            "credential.email-confirmed ok for {Account}", AccountReference(credential.Email));
    }

    /// <summary>
    /// Verifies a password and, on success, rotates the refresh token and clears any failure state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The order of the three checks is load-bearing (design decisions D-9, and AC-7):
    /// </para>
    /// <list type="number">
    /// <item>an unknown email still verifies against a dummy hash, so it costs the same as a real one
    /// (removing this reintroduces timing-based user enumeration);</item>
    /// <item>the <b>lock is checked before <c>BCrypt.Verify</c></b>, because a locked account that spent
    /// 262 ms per attempt would turn the lock into an amplifier for the denial of service it exists
    /// beside;</item>
    /// <item>the failure counter is written <b>only</b> on the verify-failed path, so a locked account
    /// takes no further writes — which is also how a test can prove the short circuit fired.</item>
    /// </list>
    /// <para>
    /// All three refusals raise the same <see cref="UnauthorizedException"/> with the same message.
    /// A distinct code or body for "locked" would tell an attacker which addresses exist and which they
    /// have successfully locked (PRD requirement 12).
    /// </para>
    /// </remarks>
    public async Task<TokenResponse?> LoginAsync(string email, string password)
    {
        email = email.ToLowerInvariant();
        var account = AccountReference(email);

        CredentialEntity? credential;
        try
        {
            credential = await repository.FindOneAsync(new BsonDocument("email", email));
        }
        catch (Exception ex) when (IsMongoDown(ex))
        {
            throw new ServiceUnavailableException();
        }

        if (credential is null)
        {
            // Constant-time dummy hash to prevent timing-based user enumeration
            BCrypt.Net.BCrypt.Verify(password, DummyHash);
            _log.LogInformation("credential.login-failed no-such-account for {Account}", account);
            throw new UnauthorizedException();
        }

        if (IsLocked(credential))
        {
            _log.LogInformation(
                "credential.login-failed locked for {Account} until {LockUntil:O}",
                account, credential.LockUntil);
            throw new UnauthorizedException();
        }

        if (!BCrypt.Net.BCrypt.Verify(password, credential.PasswordHash))
        {
            await CountFailedAttemptAsync(email, account);
            throw new UnauthorizedException();
        }

        if (!credential.EmailVerified)
        {
            _log.LogInformation("credential.login-blocked email-unverified for {Account}", account);
            throw new EmailVerificationRequiredException();
        }

        // The password is correct, but the account is flagged for a forced reset — a migration-seeded
        // stub (SeedAuthCredentials.cs) that has never had a real password chosen. No session is issued;
        // the client must call the password-reset endpoints before it can log in normally.
        if (credential.MustResetPassword)
        {
            _log.LogInformation("credential.login-blocked must-reset for {Account}", account);
            throw new PasswordResetRequiredException();
        }

        // Read before the write, so the log can say whether this login cleared anything.
        var clearedFailures = credential.FailedAttempts;
        var clearedLock = credential.LockUntil is not null;

        var privateKeyPem = ReadPrivateKeyPem();
        var (refreshOpaque, refreshHash) = CreateRefreshToken();
        var expiry = clock.UtcNow.AddHours(24);

        // One targeted write does three things: rotates the refresh token, resets the counter (AC-10)
        // and clears any stale lock. The rotation write already had to happen, so the reset is free —
        // better than the PRD's "at most one extra write", and it can never replace the document.
        try
        {
            await repository.FindOneAndUpdateAsync(
                new BsonDocument("email", email),
                new BsonDocument
                {
                    {
                        "$set", new BsonDocument
                        {
                            { "refresh_token", RefreshTokenSubdocument(refreshHash, expiry) },
                            { "failed_attempts", 0 }
                        }
                    },
                    { "$unset", new BsonDocument("lock_until", "") }
                });
        }
        catch (Exception ex) when (IsMongoDown(ex))
        {
            throw new ServiceUnavailableException();
        }

        if (clearedFailures > 0 || clearedLock)
        {
            _log.LogInformation(
                "credential.reset ok for {Account}: cleared {FailedAttempts} consecutive failures, "
                + "lock cleared {LockCleared}", account, clearedFailures, clearedLock);
        }

        _log.LogInformation("credential.login-ok for {Account}", account);

        return new TokenResponse(CreateAccessToken(privateKeyPem, email, credential.Role), refreshOpaque);
    }

    /// <summary>
    /// Exchanges a refresh token for a new pair, atomically and without ever deleting the credential.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This method used to be able to destroy an account.</b> It called
    /// <c>FindOneAndDeleteAsync</c> on the whole document and re-inserted it, so a fault between the two
    /// — including one caught by <c>IsMongoDown</c>, making the destructive path the <i>handled</i> path
    /// — lost the email, password hash, role and reset flag with no audit trail and no log line. The
    /// atomic delete was a correct single-use guard; its granularity was the defect.
    /// </para>
    /// <para>
    /// One round trip now does all of it. Single use is preserved by putting the presented hash in the
    /// <b>filter</b>: the update matches only while that hash is still stored, so a replayed token
    /// matches nothing (AC-3). The expiry check and the "account not locked" check (AC-4)
    /// ride in the same filter, costing no extra query.
    /// </para>
    /// <para>
    /// The private key is read <b>before</b> the write. Minting the access token needs the email and role
    /// that only the matched document can supply, so the write has to come first — and if reading the key
    /// then failed, the client would have burned its refresh token for nothing.
    /// </para>
    /// </remarks>
    public async Task<TokenResponse?> RefreshAsync(string refreshToken)
    {
        var presentedHash = HashToken(refreshToken);
        var now = clock.UtcNow;

        // Read WITHOUT throwing, deliberately. The key has to be in hand before the write, or a key
        // failure discovered afterwards would have already consumed the client's refresh token — but
        // throwing here would answer 500 to a request carrying a *bogus* token, which owes no such
        // answer. The integration harness caught exactly that: it hosts Identity with no
        // JWT_PRIVATE_KEY (CryptoSessionFixture never materialises a private key as a string,
        // AC-3), and every rejected refresh came back 500 instead of 401. No unit test could see it —
        // they all set the variable in their constructor.
        var privateKeyPem = Environment.GetEnvironmentVariable(PrivateKeyEnvVar)?.Replace("\\n", "\n");
        var (refreshOpaque, refreshHash) = CreateRefreshToken();

        CredentialEntity? credential;
        try
        {
            var filter = new BsonDocument
            {
                { "refresh_token.hash", presentedHash },
                { "refresh_token.expiry", new BsonDocument("$gt", now) },
                { "email_verified", true },
                { "$or", NotLocked(now) }
            };

            credential = await repository.FindOneAndUpdateAsync(
                filter,
                new BsonDocument(
                    "$set",
                    new BsonDocument(
                        "refresh_token", RefreshTokenSubdocument(refreshHash, now.AddHours(24)))));
        }
        catch (Exception ex) when (IsMongoDown(ex))
        {
            throw new ServiceUnavailableException();
        }

        if (credential is null)
        {
            // Deliberately no account reference: there is no account to name. The token did not match
            // an unlocked credential holding it, and the four reasons for that — unknown, expired,
            // already used, locked — are one outcome to the caller.
            _log.LogInformation("credential.rotated refused: no unlocked account holds that token");
            throw new UnauthorizedException("Refresh token is invalid or expired.");
        }

        // A matched rotation with no signing key is a misconfigured service, not a bad request: every
        // login and register is equally broken, so there is no session left to protect by declining to
        // consume the token.
        if (privateKeyPem is null)
        {
            throw new ApplicationException(
                $"Required environment variable '{PrivateKeyEnvVar}' is not set.");
        }

        _log.LogInformation("credential.rotated ok for {Account}", AccountReference(credential.Email));

        return new TokenResponse(
            CreateAccessToken(privateKeyPem, credential.Email, credential.Role), refreshOpaque);
    }

    public async Task LogoutAsync(string refreshToken, string? accessToken = null)
    {
        var hash = HashToken(refreshToken);

        CredentialEntity? credential;
        try
        {
            // Idempotent, and targeted: unsetting the sub-document cannot disturb the rest of the
            // credential, which a whole-document replacement built from a stale read could.
            credential = await repository.FindOneAndUpdateAsync(
                new BsonDocument("refresh_token.hash", hash),
                new BsonDocument("$unset", new BsonDocument("refresh_token", "")));
        }
        catch (Exception ex) when (IsMongoDown(ex))
        {
            throw new ServiceUnavailableException();
        }

        if (credential is not null)
        {
            _log.LogInformation(
                "credential.session-ended ok for {Account}", AccountReference(credential.Email));
        }

        await RevokeAccessTokenAsync(accessToken);
    }

    /// <summary>
    /// Denylists the caller's own access token so it stops working immediately, instead of
    /// staying valid for up to its full 60-minute lifetime after logout. The token is decoded,
    /// not re-validated — a caller can only submit a token it already legitimately holds (or a
    /// garbage string, which decodes to nothing and is silently skipped, same as no token at
    /// all); there is no signature to re-check here that every other service doesn't already
    /// enforce on the next request that would have used it.
    /// </summary>
    private async Task RevokeAccessTokenAsync(string? accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken) || tokenRevocationStore is null)
            return;

        JwtSecurityToken jwt;
        try
        {
            jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        }
        catch (ArgumentException)
        {
            return;
        }

        var jti = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;
        if (string.IsNullOrEmpty(jti))
            return;

        await tokenRevocationStore.RevokeAsync(jti, new DateTimeOffset(jwt.ValidTo, TimeSpan.Zero));
    }

    /// <summary>
    /// Issues a single-use password-reset token for the account, if one exists.
    /// </summary>
    /// <remarks>
    /// Always returns the same way regardless of whether the address matched an account (anti-
    /// enumeration, same principle as <see cref="LoginAsync"/>'s constant-time dummy hash) — including the
    /// return value, which is present only so this method is testable; the API layer must never forward
    /// it to an HTTP caller. The targeted <c>FindOneAndUpdateAsync</c> never upserts, so an unknown
    /// address writes nothing (ADR-032).
    /// <para>
    /// No real email/SMS provider exists in this project (same category as no real payment gateway,
    /// ADR-038) — the reset link is logged at Information (visible in the Aspire dashboard, dev-only
    /// channel) rather than actually delivered. A <see cref="NotificationEntity"/> is also written via
    /// the existing in-app inbox as a secondary, audit-trail signal — not the primary channel, since that
    /// inbox itself requires authentication and is therefore unreachable by the very user who is locked
    /// out (ADR-052).
    /// </para>
    /// </remarks>
    /// <returns>
    /// The raw opaque token if an account matched, for tests and local-dev logging only — the HTTP
    /// endpoint must discard this rather than including it in a response.
    /// </returns>
    public async Task<string?> RequestPasswordResetAsync(string email)
    {
        email = email.ToLowerInvariant();
        var account = AccountReference(email);
        var (resetOpaque, resetHash) = CreateRefreshToken();
        var expiry = clock.UtcNow.Add(ResetTokenLifetime);

        CredentialEntity? credential;
        try
        {
            credential = await repository.FindOneAndUpdateAsync(
                new BsonDocument("email", email),
                new BsonDocument("$set", new BsonDocument("reset_token", ResetTokenSubdocument(resetHash, expiry))));
        }
        catch (Exception ex) when (IsMongoDown(ex))
        {
            throw new ServiceUnavailableException();
        }

        if (credential is null) return null; // No such account — nothing written, nothing to notify.

        // The token itself is never logged. It grants a password change to whoever holds it, so a log
        // sink is a place it can be read by anyone with log access and used to take over the account.
        _log.LogInformation(
            "credential.password-reset-requested for {Account}: expires {Expiry:O}",
            account, expiry);

        if (notificationService is not null)
        {
            await notificationService.SendAsync(new NotificationEntity(
                recipientEmail: credential.Email,
                subject: "Password reset requested",
                body: "A password reset was requested for your account. If this wasn't you, "
                      + "you can ignore this — no change takes effect until a new password is confirmed.",
                type: NotificationType.PasswordResetRequested,
                appointmentIdentifier: string.Empty));
        }

        // Email is the only channel that works here: the whole point of a reset is reaching someone who
        // cannot sign in, so an in-app inbox row is unreachable by definition.
        if (emailSender is not null)
        {
            await emailSender.SendAsync(credential.Email, "Reset your password", BuildResetEmail(resetOpaque));
        }

        return resetOpaque;
    }

    /// <summary>
    /// Consumes a single-use reset token and sets a new password.
    /// </summary>
    /// <remarks>
    /// Filter-based single use, same technique as <see cref="RefreshAsync"/>: the presented hash and a
    /// not-yet-expired expiry both ride the filter, so a replayed or expired token matches nothing rather
    /// than being checked and rejected after the fact. On success, every other credential-security state
    /// is cleared in the same write: the reset flag, any lockout, and the active refresh token — a
    /// successful reset ends every existing session, the same posture a real "forgot password" flow takes
    /// elsewhere, since the old password (and anything authenticated under it) can no longer be trusted.
    /// </remarks>
    public async Task ConfirmPasswordResetAsync(string email, string token, string newPassword)
    {
        email = email.ToLowerInvariant();
        var account = AccountReference(email);

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
            throw new AuthValidationException("Password must be at least 8 characters.");

        var presentedHash = HashToken(token);
        var now = clock.UtcNow;
        var newHash = BCrypt.Net.BCrypt.HashPassword(newPassword, workFactor: 12);

        CredentialEntity? credential;
        try
        {
            var filter = new BsonDocument
            {
                { "email", email },
                { "reset_token.hash", presentedHash },
                { "reset_token.expiry", new BsonDocument("$gt", now) }
            };

            credential = await repository.FindOneAndUpdateAsync(
                filter,
                new BsonDocument
                {
                    {
                        "$set", new BsonDocument
                        {
                            { "password_hash", newHash },
                            { "must_reset_password", false }
                        }
                    },
                    {
                        "$unset", new BsonDocument
                        {
                            { "reset_token", "" }, { "refresh_token", "" }, { "lock_until", "" }
                        }
                    }
                });
        }
        catch (Exception ex) when (IsMongoDown(ex))
        {
            throw new ServiceUnavailableException();
        }

        if (credential is null)
        {
            // Deliberately no account reference, same reasoning as RefreshAsync: unknown, expired,
            // already-used and wrong-email are all one outcome to the caller.
            _log.LogInformation("credential.password-reset-confirm refused: no unlocked token match");
            throw new UnauthorizedException("Reset token is invalid or expired.");
        }

        _log.LogInformation("credential.password-reset-confirmed ok for {Account}", account);
    }

    /// <summary>
    /// Counts one failed attempt and applies the lock if that attempt reached the threshold.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two writes, not one, and never a read-modify-write (AC-11). The increment is an
    /// atomic <c>$inc</c>, so concurrent attempts cannot lose a count; the lock is a second, conditional
    /// update that runs on one attempt in N.
    /// </para>
    /// <para>
    /// The lock update repeats the threshold as a filter condition even though the returned counter has
    /// already been checked, so two racing attempts cannot produce a lock the counter does not justify.
    /// </para>
    /// <para>
    /// Neither write can create a document: the primitive never upserts, so counting a failure against an
    /// address with no account writes nothing (AC-9). That matters because this is an
    /// <b>unauthenticated</b> write path on a collection with no backups, which is why the per-IP limiter
    /// is evaluated before it ever runs (PRD requirement 11).
    /// </para>
    /// </remarks>
    private async Task CountFailedAttemptAsync(string email, string account)
    {
        CredentialEntity? updated;
        try
        {
            updated = await repository.FindOneAndUpdateAsync(
                new BsonDocument("email", email),
                new BsonDocument("$inc", new BsonDocument("failed_attempts", 1)));
        }
        catch (Exception ex) when (IsMongoDown(ex))
        {
            throw new ServiceUnavailableException();
        }

        if (updated is null)
        {
            // The account was removed between the read and this write. Nothing to count, nothing to
            // lock, and — the point of the primitive — nothing recreated.
            _log.LogInformation("credential.login-failed vanished-account for {Account}", account);
            return;
        }

        _log.LogInformation(
            "credential.login-failed wrong-password for {Account}, {FailedAttempts} consecutive",
            account, updated.FailedAttempts);

        if (updated.FailedAttempts < _lockout.MaxFailedAttempts) return;

        var lockUntil = clock.UtcNow.AddMinutes(_lockout.WindowMinutes);

        try
        {
            await repository.FindOneAndUpdateAsync(
                new BsonDocument
                {
                    { "email", email },
                    { "failed_attempts", new BsonDocument("$gte", _lockout.MaxFailedAttempts) }
                },
                new BsonDocument("$set", new BsonDocument("lock_until", lockUntil)));
        }
        catch (Exception ex) when (IsMongoDown(ex))
        {
            throw new ServiceUnavailableException();
        }

        _log.LogInformation(
            "credential.locked for {Account} until {LockUntil:O} after {FailedAttempts} consecutive "
            + "failures", account, lockUntil, updated.FailedAttempts);
    }

    /// <summary>
    /// Whether a lock is currently in force. A <c>lock_until</c> in the past reads as unlocked, and
    /// clearing it costs no write and needs no sweeper (AC-8, AC-9).
    /// </summary>
    private bool IsLocked(CredentialEntity credential) =>
        credential.LockUntil is { } lockUntil && lockUntil > clock.UtcNow;

    /// <summary>
    /// The "account is not locked" half of a filter: <c>lock_until</c> absent, null, or in the past.
    /// </summary>
    /// <remarks>
    /// Both branches are required. In MongoDB a missing field satisfies no comparison operator, so
    /// <c>lock_until &lt;= now</c> alone would never match an account that has never been locked — which
    /// is every account, almost all of the time.
    /// </remarks>
    private static BsonArray NotLocked(DateTime now) =>
    [
        new BsonDocument("lock_until", BsonNull.Value),
        new BsonDocument("lock_until", new BsonDocument("$lte", now))
    ];

    private static BsonDocument RefreshTokenSubdocument(string hash, DateTime expiry) =>
        new() { { "hash", hash }, { "expiry", expiry } };

    private static BsonDocument ResetTokenSubdocument(string hash, DateTime expiry) =>
        new() { { "hash", hash }, { "expiry", expiry } };

    /// <summary>
    /// Bilingual confirmation message. The app link is convenient when AgendaMe is installed; the code is
    /// the universal fallback and is bound to the registered email at confirmation time.
    /// </summary>
    private EmailContent BuildConfirmationEmail(string token, string code)
    {
        var link = $"agendame://email/confirm-email?token={Uri.EscapeDataString(token)}";
        var htmlLink = System.Net.WebUtility.HtmlEncode(link);
        var text = $"""
                    Welcome to AgendaMe!

                    Thank you for signing up. To complete your registration and secure your account, verify your email address using one of the methods below.

                    Option 1: Click the verification button
                    Verify my email: {link}
                    The verification button expires in 24 hours.

                    Option 2: Enter this 6-digit verification code
                    {code}
                    This verification code expires in 15 minutes.

                    If you did not create an AgendaMe account, please disregard this email. No further action is required.

                    Thank you,
                    The AgendaMe Team

                    ------------------------------------------------------------

                    ¡Bienvenido a AgendaMe!

                    Gracias por registrarte. Para completar tu registro y proteger tu cuenta, verifica tu correo electrónico usando uno de los siguientes métodos.

                    Opción 1: Haz clic en el botón de verificación
                    Verificar mi correo: {link}
                    El botón de verificación vence en 24 horas.

                    Opción 2: Ingresa este código de verificación de 6 dígitos
                    {code}
                    Este código de verificación vence en 15 minutos.

                    Si no creaste una cuenta de AgendaMe, ignora este correo. No es necesario realizar ninguna otra acción.

                    Gracias,
                    El equipo de AgendaMe
                    """;
        var html = $"""
                    <!doctype html>
                    <html lang="en">
                    <head>
                        <meta name="viewport" content="width=device-width, initial-scale=1">
                        <meta name="color-scheme" content="light">
                        <title>Welcome to AgendaMe - Verify your email address</title>
                    </head>
                    <body style="margin:0;background:#eef2ef;color:#17211b;font-family:Verdana,Geneva,sans-serif;line-height:1.6">
                        <div style="display:none;max-height:0;overflow:hidden;color:transparent">Verify your email address to complete your AgendaMe registration.</div>
                        <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#eef2ef;padding:28px 12px">
                            <tr>
                                <td align="center">
                                    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:640px;background:#ffffff;border:1px solid #d6dfd9;border-radius:8px;overflow:hidden">
                                        <tr>
                                            <td style="background:#123f32;padding:26px 32px;color:#ffffff">
                                                <div style="font-family:Georgia,serif;font-size:30px;font-weight:bold">Agenda<span style="color:#f2bd4d">Me</span></div>
                                                <div style="font-size:13px;color:#dbe8e1;margin-top:4px">Your schedule. Your clients. One place.</div>
                                            </td>
                                        </tr>
                                        <tr>
                                            <td style="padding:32px">
                                                <div lang="en">
                                                    <h1 style="margin:0 0 12px;font-family:Georgia,serif;font-size:25px;color:#123f32">Welcome to AgendaMe!</h1>
                                                    <p style="margin:0 0 24px">Thank you for signing up. To complete your registration and secure your account, please verify your email address using one of the methods below.</p>

                                                    <div style="border-left:4px solid #23805e;background:#f3f8f5;padding:18px 20px;margin:0 0 16px">
                                                        <div style="font-size:12px;font-weight:bold;color:#176b4d">OPTION 1</div>
                                                        <h2 style="margin:3px 0 14px;font-size:17px;color:#17211b">Click the verification button</h2>
                                                        <a href="{htmlLink}" style="display:inline-block;background:#176b4d;color:#ffffff;padding:12px 20px;text-decoration:none;border-radius:6px;font-weight:bold">Verify my email</a>
                                                        <p style="margin:12px 0 0;font-size:13px;color:#526158">The verification button expires in 24 hours.</p>
                                                    </div>

                                                    <div style="border-left:4px solid #e4a72c;background:#fff9eb;padding:18px 20px;margin:0 0 24px">
                                                        <div style="font-size:12px;font-weight:bold;color:#85600e">OPTION 2</div>
                                                        <h2 style="margin:3px 0 10px;font-size:17px;color:#17211b">Enter this 6-digit verification code</h2>
                                                        <div style="font-family:'Courier New',monospace;font-size:32px;font-weight:bold;color:#123f32">{code}</div>
                                                        <p style="margin:8px 0 0;font-size:13px;color:#6b5522">This verification code expires in 15 minutes.</p>
                                                    </div>

                                                    <p style="margin:0 0 22px;font-size:14px;color:#526158">If you did not create an AgendaMe account, please disregard this email. No further action is required.</p>
                                                    <p style="margin:0">Thank you,<br><strong>The AgendaMe Team</strong></p>
                                                </div>

                                                <hr style="border:0;border-top:1px solid #d9dedb;margin:32px 0">

                                                <div lang="es">
                                                    <h1 style="margin:0 0 12px;font-family:Georgia,serif;font-size:25px;color:#123f32">¡Bienvenido a AgendaMe!</h1>
                                                    <p style="margin:0 0 24px">Gracias por registrarte. Para completar tu registro y proteger tu cuenta, verifica tu correo electrónico usando uno de los siguientes métodos.</p>

                                                    <div style="border-left:4px solid #23805e;background:#f3f8f5;padding:18px 20px;margin:0 0 16px">
                                                        <div style="font-size:12px;font-weight:bold;color:#176b4d">OPCIÓN 1</div>
                                                        <h2 style="margin:3px 0 14px;font-size:17px;color:#17211b">Haz clic en el botón de verificación</h2>
                                                        <a href="{htmlLink}" style="display:inline-block;background:#176b4d;color:#ffffff;padding:12px 20px;text-decoration:none;border-radius:6px;font-weight:bold">Verificar mi correo</a>
                                                        <p style="margin:12px 0 0;font-size:13px;color:#526158">El botón de verificación vence en 24 horas.</p>
                                                    </div>

                                                    <div style="border-left:4px solid #e4a72c;background:#fff9eb;padding:18px 20px;margin:0 0 24px">
                                                        <div style="font-size:12px;font-weight:bold;color:#85600e">OPCIÓN 2</div>
                                                        <h2 style="margin:3px 0 10px;font-size:17px;color:#17211b">Ingresa este código de verificación de 6 dígitos</h2>
                                                        <div style="font-family:'Courier New',monospace;font-size:32px;font-weight:bold;color:#123f32">{code}</div>
                                                        <p style="margin:8px 0 0;font-size:13px;color:#6b5522">Este código de verificación vence en 15 minutos.</p>
                                                    </div>

                                                    <p style="margin:0 0 22px;font-size:14px;color:#526158">Si no creaste una cuenta de AgendaMe, ignora este correo. No es necesario realizar ninguna otra acción.</p>
                                                    <p style="margin:0">Gracias,<br><strong>El equipo de AgendaMe</strong></p>
                                                </div>
                                            </td>
                                        </tr>
                                    </table>
                                </td>
                            </tr>
                        </table>
                    </body>
                    </html>
                    """;

        return new EmailContent(text, html);
    }

    private async Task SendConfirmationEmailAsync(string email, string token, string code)
    {
        if (emailSender is null) return;

        var message = BuildConfirmationEmail(token, code);
        await emailSender.SendAsync(
            email,
            "Welcome to AgendaMe - Verify your email address / Bienvenido a AgendaMe - Verifica tu correo electrónico",
            message.Text,
            message.Html);
    }

    private sealed record EmailContent(string Text, string Html);

    /// <summary>
    /// Reset message. Deliberately does not state whether the address had an account -- the route answers
    /// 202 either way, and this message is only ever sent when one exists.
    /// </summary>
    private string BuildResetEmail(string token) =>
        string.IsNullOrWhiteSpace(_email.AppLinkBaseUrl)
            ? $"A password reset was requested for your AgendaMe account.\n\nUse this code:\n\n{token}\n\n"
              + "It expires in 30 minutes. If this wasn't you, ignore this message -- nothing changes until a "
              + "new password is confirmed."
            : $"A password reset was requested for your AgendaMe account.\n\nReset it here:\n\n"
              + $"{_email.AppLinkBaseUrl!.TrimEnd('/')}/reset-password?token={token}\n\n"
              + "The link expires in 30 minutes. If this wasn't you, ignore this message -- nothing changes "
              + "until a new password is confirmed.";

    private (string accessToken, string refreshOpaque, string refreshHash) GenerateTokenPair(
        string email, string role)
    {
        var (refreshOpaque, refreshHash) = CreateRefreshToken();
        return (CreateAccessToken(ReadPrivateKeyPem(), email, role), refreshOpaque, refreshHash);
    }

    /// <summary>
    /// Reads and normalises the signing key from the environment.
    /// </summary>
    /// <remarks>
    /// Split out of <see cref="GenerateTokenPair"/> so a caller can fail on a missing key <b>before</b>
    /// writing to the database. Rotation cannot mint its access token until the update tells it whose
    /// credential matched, and a key error discovered at that point would have already consumed the
    /// client's refresh token.
    /// </remarks>
    private static string ReadPrivateKeyPem() =>
        (Environment.GetEnvironmentVariable(PrivateKeyEnvVar)
         ?? throw new ApplicationException(
             $"Required environment variable '{PrivateKeyEnvVar}' is not set."))
        .Replace("\\n", "\n");

    private string CreateAccessToken(string privateKeyPem, string email, string role)
    {
        var now = clock.UtcNow;
        var issuedAt = new DateTimeOffset(DateTime.SpecifyKind(now, DateTimeKind.Utc));
        var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);
        var signingKey = new RsaSecurityKey(rsa);
        var signingCreds = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, email),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(
                JwtRegisteredClaimNames.Iat,
                issuedAt.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
                ClaimValueTypes.Integer64),
        };

        var token = new JwtSecurityToken(
            issuer: Issuer,
            claims: claims,
            expires: now.AddMinutes(60),
            signingCredentials: signingCreds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// A fresh opaque refresh token and the SHA-256 hash that is all the database ever holds.
    /// </summary>
    private static (string opaque, string hash) CreateRefreshToken()
    {
        var opaque = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return (opaque, HashToken(opaque));
    }

    private static string CreateEmailVerificationCode() =>
        RandomNumberGenerator.GetInt32(1_000_000)
            .ToString("D6", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// A one-way, log-safe handle for an account: <c>acct_</c> plus the first 12 hex characters of
    /// SHA-256 over the lower-cased address.
    /// </summary>
    /// <remarks>
    /// Design decision D-8. Email is PII under <c>CONSTITUTION.md</c> §4, and
    /// <c>PiiRedactingProcessor</c> redacts <b>spans, not logs</b> — so an address written here would
    /// reach the Aspire dashboard and any future aggregator with nothing downstream to catch it. This
    /// project's own telemetry rollout is precedent: it began exporting real customer emails in
    /// <c>url.path</c> the moment it was switched on.
    /// <para>
    /// A prefix, not the whole digest, because the point is correlating one account's mutations in a log,
    /// not resisting a dictionary attack — with a known address list any full hash is reversible anyway,
    /// which is why the honest claim is "not an address", not "anonymous".
    /// </para>
    /// </remarks>
    public static string AccountReference(string email) =>
        "acct_" + Convert.ToHexString(
                SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(email.ToLowerInvariant())))
            .ToLowerInvariant()[..12];

    /// <summary>
    /// Deletes the credential for <paramref name="email"/>, releases its device registration, and revokes the
    /// access token that authorised the call.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <b>credential half</b> of account deletion. The domain profile lives in a different database and is
    /// removed by <c>DELETE /api/v1/{customers|providers}/{email}</c>, which the client calls <b>first</b> —
    /// this credential is what authorises that call, so reversing the order would strand a live profile nothing
    /// could reach to finish erasing.
    /// </para>
    /// <para>
    /// <b>Idempotent, and it never reports whether an account existed.</b> A deletion that answered differently
    /// for a known and an unknown address would be an enumeration oracle on the one route guaranteed to be
    /// reachable by an authenticated caller — the same reasoning that makes <c>DELETE /device-token</c> answer
    /// 204 either way.
    /// </para>
    /// <para>
    /// The device registration goes <b>before</b> the credential, and that order is load-bearing for the same
    /// reason <c>AuthService.LogoutAsync</c>'s is: a fault between the two leaves an account that can still sign
    /// in and is still addressable, rather than a deleted account whose device keeps receiving push — subject and
    /// body included — on hardware it no longer controls. The domain-side erasure deletes the same registration,
    /// so this is deliberately belt and braces: a client that reached here without completing the domain half
    /// still stops being pushed to.
    /// </para>
    /// </remarks>
    public async Task DeleteAccountAsync(string email, string? accessToken = null)
    {
        email = email.ToLowerInvariant();
        var account = AccountReference(email);

        if (deviceTokenService is not null)
        {
            try
            {
                await deviceTokenService.DeleteByEmailAsync(email);
            }
            catch (Exception ex) when (IsMongoDown(ex))
            {
                // Deliberately not fatal: the registration is in a different collection from the credential, and
                // an account that cannot be deleted because a push table was briefly unreachable is a worse
                // outcome than a stale token row. The credential delete below is what makes sign-in stop.
                _log.LogWarning(ex, "credential.device-token-release-failed for {Account}", account);
            }
        }

        long deleted;
        try
        {
            // DeleteMany on a strict email filter rather than FindOneAndDelete: it is naturally idempotent, and
            // it removes a duplicate credential too. There is no unique index on email (agenda-buddy-b0w), so
            // duplicates are possible, and a delete that left one of them behind would leave the account able to
            // sign in after being told it was gone.
            deleted = await repository.DeleteManyAsync(new BsonDocument("email", email));
        }
        catch (Exception ex) when (IsMongoDown(ex))
        {
            throw new ServiceUnavailableException();
        }

        _log.LogInformation(
            "credential.deleted for {Account} ({Count} credential(s))", account, deleted);

        await RevokeAccessTokenAsync(accessToken);
    }

    public static string HashToken(string token)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool IsMongoDown(Exception ex) =>
        ex is MongoConnectionException or MongoException or TimeoutException;
}

public class AuthValidationException(string message) : Exception(message);
public class ConflictException(string message) : Exception(message);
public class UnauthorizedException(string message = "Invalid credentials.") : Exception(message);
public class ServiceUnavailableException() : Exception("Authentication service temporarily unavailable.");
public class PasswordResetRequiredException() : Exception("Password reset required before login.");

public class EmailVerificationRequiredException() : Exception("Email verification required before login.");
