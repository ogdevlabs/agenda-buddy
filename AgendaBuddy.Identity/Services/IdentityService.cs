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
    ITokenRevocationStore? tokenRevocationStore = null)
{
    private const string PrivateKeyEnvVar = "JWT_PRIVATE_KEY";
    private const string Issuer = "agenda-buddy-identity";
    private static readonly string[] AllowedRoles = ["Provider", "Customer"];
    private static readonly TimeSpan ResetTokenLifetime = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan EmailVerificationTokenLifetime = TimeSpan.FromHours(24);

    /// <summary>
    /// Lockout thresholds. Both parameters are optional so the shipped defaults apply to any caller that
    /// has not configured them — including the 20-odd unit tests that predate this feature.
    /// </summary>
    private readonly LockoutOptions _lockout = lockoutOptions?.Value ?? new LockoutOptions();

    private readonly ILogger _log = logger ?? NullLogger<IdentityService>.Instance;

    private static readonly string DummyHash =
        BCrypt.Net.BCrypt.HashPassword(Guid.Empty.ToString(), workFactor: 12);

    public async Task<TokenResponse?> RegisterAsync(string email, string password, string role)
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
        var (accessToken, refreshOpaque, refreshHash) = GenerateTokenPair(email, role);
        var (verificationOpaque, verificationHash) = CreateRefreshToken();
        var verificationExpiry = clock.UtcNow.Add(EmailVerificationTokenLifetime);

        var credential = new CredentialEntity
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Email = email,
            PasswordHash = hash,
            Role = role,
            MustResetPassword = false,
            RefreshToken = new RefreshTokenDocument
            {
                Hash = refreshHash,
                Expiry = clock.UtcNow.AddHours(24)
            },
            EmailVerified = false,
            EmailVerificationToken = new EmailVerificationTokenDocument
            {
                Hash = verificationHash,
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

        // No email provider is configured (ADR-052) — logged for local development the same way a
        // password-reset token is, not gated on for login (see EmailVerified's own remarks).
        _log.LogInformation(
            "credential.email-confirmation-requested for {Account}: token={ConfirmationToken} expires {Expiry:O}",
            AccountReference(email), verificationOpaque, verificationExpiry);

        if (notificationService is not null)
        {
            await notificationService.SendAsync(new NotificationEntity(
                recipientEmail: email,
                subject: "Confirm your email address",
                body: "Welcome to Agenda Buddy. Please confirm you own this email address to finish setting up your account.",
                type: NotificationType.EmailConfirmationRequested,
                appointmentIdentifier: string.Empty));
        }

        return new TokenResponse(accessToken, refreshOpaque, verificationOpaque);
    }

    /// <summary>
    /// Consumes a single-use email-confirmation token. Not required for login (see
    /// <see cref="CredentialEntity.EmailVerified"/>'s own remarks) — this only flips that flag.
    /// </summary>
    public async Task ConfirmEmailAsync(string email, string token)
    {
        email = email.ToLowerInvariant();
        var account = AccountReference(email);
        var presentedHash = HashToken(token);
        var now = clock.UtcNow;

        CredentialEntity? credential;
        try
        {
            var filter = new BsonDocument
            {
                { "email", email },
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

        _log.LogInformation("credential.email-confirmed ok for {Account}", account);
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

        _log.LogInformation(
            "credential.password-reset-requested for {Account}: token={ResetToken} expires {Expiry:O} "
            + "(no email provider configured — logged for local development only, see ADR-052)",
            account, resetOpaque, expiry);

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
        var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);
        var signingKey = new RsaSecurityKey(rsa);
        var signingCreds = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, email),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: Issuer,
            claims: claims,
            expires: clock.UtcNow.AddMinutes(60),
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
