using System.Security.Cryptography;

namespace AgendaBuddy.Library.Accounts;

/// <summary>
/// The identifiers a deleted account's traces are rewritten to.
/// </summary>
/// <remarks>
/// <para>
/// Deleting an account cannot mean deleting the counterparty's records: a provider who delivered and was paid for
/// twenty sessions must keep twenty sessions, and a conversation with one side removed is not a conversation. So
/// the dependent rows survive with every identifying field replaced by a tombstone.
/// </para>
/// <para>
/// <b>The tombstone is random, not derived from the address.</b> A hash of the email would keep the row linkable
/// back to the person by testing guesses — the address space of real email addresses is small enough to enumerate
/// against a known hash, so a derived tombstone is a re-identification vector, not an erasure. One random token is
/// minted per deletion and used for every field of it, which keeps a provider's history internally coherent — the
/// twenty sessions still read as twenty sessions with one person — while retaining nothing about who that was.
/// </para>
/// <para>
/// <c>.invalid</c> is reserved by RFC 2606 and can never resolve, so a tombstone that leaks into a mail or push
/// send cannot reach a real inbox.
/// </para>
/// <para>
/// Free of MongoDB and MAUI types so both sides can assert on it.
/// </para>
/// </remarks>
public static class AccountErasure
{
    /// <summary>The reserved domain every tombstone address sits in.</summary>
    public const string TombstoneDomain = "deleted.invalid";

    /// <summary>The local-part prefix, so a tombstone is recognisable on sight in a database or a log.</summary>
    private const string TombstonePrefix = "deleted-user-";

    /// <summary>What a deleted account's name reads as wherever one is shown.</summary>
    public const string TombstoneFirstName = "Deleted";

    /// <summary>The second half of the name, so surfaces that join first and last read "Deleted account".</summary>
    public const string TombstoneLastName = "account";

    /// <summary>
    /// A fresh tombstone address. Mint <b>one per deletion</b> and reuse it across every field being scrubbed.
    /// </summary>
    /// <remarks>
    /// 32 bits of randomness. It does not need to be unguessable — nothing authenticates against it — only
    /// unlinkable to the address it replaces and unlikely to collide with another deletion's.
    /// </remarks>
    public static string NewTombstone() =>
        $"{TombstonePrefix}{RandomNumberGenerator.GetHexString(8, lowercase: true)}@{TombstoneDomain}";

    /// <summary>
    /// Whether <paramref name="email"/> is a tombstone rather than a real address.
    /// </summary>
    /// <remarks>
    /// The client needs this to draw "Deleted account" instead of the raw tombstone, and the erasure service
    /// needs it to stay idempotent — a second delete of an already-erased account must not scrub a second time
    /// under a new token, which would split one person's history into two.
    /// </remarks>
    public static bool IsTombstone(string? email) =>
        email is not null
        && email.EndsWith($"@{TombstoneDomain}", StringComparison.OrdinalIgnoreCase)
        && email.StartsWith(TombstonePrefix, StringComparison.OrdinalIgnoreCase);
}
