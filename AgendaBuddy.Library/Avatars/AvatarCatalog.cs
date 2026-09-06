using System.Security.Cryptography;
using System.Text;

namespace AgendaBuddy.Library.Avatars;

/// <summary>
/// The fixed set of avatars an account can be assigned, and the rules for picking one.
/// </summary>
/// <remarks>
/// <para>
/// Lives in <c>AgendaBuddy.Library</c> because both sides need the same answer: the server assigns an avatar
/// when a profile is created, and <c>AgendaBuddy.MobileApp</c> turns the stored id into an image asset. A
/// separate list on each side would drift into the client asking for an asset that does not exist, which
/// renders as an empty circle rather than as an error.
/// </para>
/// <para>
/// <b>Why not initials.</b> A letter in a circle is not an avatar — it is the name again, smaller, and it makes
/// every account whose name starts with the same letter look identical in a list. It is also actively wrong for
/// this product: a provider's client list is a list of real people, and "J" tells the provider nothing they
/// cannot already read on the line beside it.
/// </para>
/// </remarks>
public static class AvatarCatalog
{
    /// <summary>How many avatars exist. Every id is <c>avatar_01</c>..<c>avatar_{Count}</c>, zero-padded to two.</summary>
    public const int Count = 24;

    /// <summary>The id prefix, shared with the generated asset file names.</summary>
    private const string Prefix = "avatar_";

    /// <summary>Every id, in order. Materialised once — the set is fixed at compile time.</summary>
    public static readonly IReadOnlyList<string> Ids =
        Enumerable.Range(1, Count).Select(IdAt).ToArray();

    /// <summary>The id at a 1-based position.</summary>
    private static string IdAt(int position) => $"{Prefix}{position:D2}";

    /// <summary>Whether a stored value names an avatar this build actually ships.</summary>
    /// <remarks>
    /// Guards the case that matters: a row written by a future build with a larger catalog, read by an older
    /// one. Without the check the client asks for a missing asset and draws nothing, which looks like a bug in
    /// the list rather than a value it cannot honour.
    /// </remarks>
    public static bool IsKnown(string? avatarId) =>
        !string.IsNullOrWhiteSpace(avatarId) && Ids.Contains(avatarId);

    /// <summary>
    /// Picks one at random, for a profile being created.
    /// </summary>
    /// <remarks>
    /// Genuinely random rather than derived from the address, so two accounts belonging to the same person do
    /// not necessarily match and a provider's client list looks like a list of individuals. The deterministic
    /// path below exists only as a fallback for rows that have no stored value.
    /// </remarks>
    public static string Random() => Ids[RandomNumberGenerator.GetInt32(Count)];

    /// <summary>
    /// The avatar for a seed — the same seed always gives the same avatar, in every process and every run.
    /// </summary>
    /// <param name="seed">Normally the account's email address.</param>
    /// <remarks>
    /// <b>SHA-256 rather than <c>string.GetHashCode()</c>, and that is load-bearing.</b> .NET randomises string
    /// hash codes per process, so a <c>GetHashCode</c>-derived avatar would change every time the app or the
    /// service restarted — the one thing an identity mark must never do. Only the first four bytes are needed;
    /// the modulo bias across 24 buckets is immaterial for a decorative assignment.
    /// </remarks>
    public static string Deterministic(string? seed)
    {
        var normalised = (seed ?? string.Empty).Trim().ToLowerInvariant();
        if (normalised.Length == 0) return Ids[0];

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(normalised));
        var value = ((uint)digest[0] << 24) | ((uint)digest[1] << 16) | ((uint)digest[2] << 8) | digest[3];

        return Ids[(int)(value % Count)];
    }

    /// <summary>
    /// The avatar to draw: the stored assignment when there is a usable one, otherwise a stable choice from the
    /// seed.
    /// </summary>
    /// <remarks>
    /// The fallback is what makes this shippable without a migration. Every account that predates avatar
    /// assignment — and any whose profile creation failed — still gets a stable, distinct mark rather than a
    /// blank circle, and it is the same mark on every screen and every device because it comes from the address.
    /// </remarks>
    public static string Resolve(string? storedAvatarId, string? seed) =>
        IsKnown(storedAvatarId) ? storedAvatarId! : Deterministic(seed);
}
