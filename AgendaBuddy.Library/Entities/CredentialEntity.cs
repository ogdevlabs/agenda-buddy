#pragma warning disable CS8618

namespace AgendaBuddy.Library.Entities;

/// <summary>MongoDB document for auth credentials. One document per user account.</summary>
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public class CredentialEntity
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }

    [Required]
    [EmailAddress]
    [BsonElement("email")]
    public string Email { get; set; }

    [Required]
    [BsonElement("password_hash")]
    public string PasswordHash { get; set; }

    /// <summary>"Provider" or "Customer" — single role per account (v1).</summary>
    [Required]
    [BsonElement("role")]
    public string Role { get; set; }

    /// <summary>True for migration-seeded stubs; user must reset password on first login.</summary>
    [BsonElement("must_reset_password")]
    public bool MustResetPassword { get; set; } = false;

    /// <summary>Embedded refresh token sub-document. Null when no active session.</summary>
    [BsonElement("refresh_token")]
    public RefreshTokenDocument? RefreshToken { get; set; }

    /// <summary>
    /// Consecutive failed logins. Reset to 0 by a successful one (AC-10).
    /// </summary>
    /// <remarks>
    /// Written only by an atomic <c>$inc</c> through
    /// <c>IRepository&lt;T&gt;.FindOneAndUpdateAsync</c> — never by a read-modify-write, and never as
    /// part of a whole-document replacement. An absent field deserializes to 0, which is exactly "this
    /// account has never failed a login", so existing documents need no migration.
    /// </remarks>
    [BsonElement("failed_attempts")]
    [BsonIgnoreIfDefault]
    public int FailedAttempts { get; set; }

    /// <summary>
    /// UTC instant a lockout expires. <c>null</c> — or any value in the past — means not locked.
    /// </summary>
    /// <remarks>
    /// Storing only the expiry makes "unlocked" the <i>absence of a future value</i>, so the lock clears
    /// itself with no write on the read path and no sweeper job (AC-8). There is deliberately no
    /// permanent lock and no administrative unlock: password reset does not exist yet, so a lock
    /// that needed a human to clear it would strand a real provider — and let an attacker strand one on
    /// purpose.
    /// </remarks>
    [BsonElement("lock_until")]
    [BsonIgnoreIfNull]
    public DateTime? LockUntil { get; set; }
}

/// <summary>Embedded sub-document storing the SHA-256 hash of the opaque refresh token.</summary>
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public class RefreshTokenDocument
{
    /// <summary>SHA-256 hex hash of the opaque token sent to the client. Raw token never stored.</summary>
    [BsonElement("hash")]
    public string Hash { get; set; }

    /// <summary>UTC expiry timestamp. TTL index on this field in MongoDB.</summary>
    [BsonElement("expiry")]
    public DateTime Expiry { get; set; }
}
