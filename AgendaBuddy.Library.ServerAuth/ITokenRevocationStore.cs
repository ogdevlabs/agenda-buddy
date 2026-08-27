namespace AgendaBuddy.Library.Extensions;

/// <summary>
/// Cross-service access-token denylist, keyed by the token's <c>jti</c> claim.
/// </summary>
public interface ITokenRevocationStore
{
    Task RevokeAsync(string jti, DateTimeOffset expiresAtUtc);

    Task<bool> IsRevokedAsync(string jti);
}
