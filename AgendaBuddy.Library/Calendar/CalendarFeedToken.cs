using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace AgendaBuddy.Library.Calendar;

/// <summary>
/// The secret in a calendar feed URL. 256 random bits, base64url without padding (43 characters), so it is safe in a
/// path segment and too large to guess. Only its SHA-256 hash is stored: a database read must not yield a live URL.
/// </summary>
public static partial class CalendarFeedToken
{
    public const int Length = 43;

    public static string New()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static bool IsWellFormed(string? token) => token is { Length: Length } && Shape().IsMatch(token);

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(token))).ToLowerInvariant();

    [GeneratedRegex("^[A-Za-z0-9_-]+$")]
    private static partial Regex Shape();
}
