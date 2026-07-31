#pragma warning disable CS8618

namespace Library.Entities;

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
