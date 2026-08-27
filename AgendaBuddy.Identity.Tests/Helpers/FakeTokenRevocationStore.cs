using AgendaBuddy.Library.Extensions;

namespace AgendaBuddy.Identity.Tests.Helpers;

public class FakeTokenRevocationStore : ITokenRevocationStore
{
    private readonly Dictionary<string, DateTimeOffset> _revoked = new();

    public IReadOnlyDictionary<string, DateTimeOffset> Revoked => _revoked;

    public Task RevokeAsync(string jti, DateTimeOffset expiresAtUtc)
    {
        _revoked[jti] = expiresAtUtc;
        return Task.CompletedTask;
    }

    public Task<bool> IsRevokedAsync(string jti) => Task.FromResult(_revoked.ContainsKey(jti));
}
