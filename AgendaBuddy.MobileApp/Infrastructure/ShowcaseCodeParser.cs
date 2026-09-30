namespace AgendaBuddy.MobileApp.Infrastructure;

/// <summary>
/// Reads a provider's public code out of whatever a customer scanned or typed.
/// </summary>
/// <remarks>
/// <para>
/// Accepts an http(s) URL whose path ends in <c>/api/v1/go/{code}</c>, on any host, or a bare code. The host is
/// never trusted and the URL is never followed: only the code is taken, so an in-app scan does not count as an
/// anonymous store visit, and a look-alike URL gains nothing because the code is resolved by the app's own API.
/// </para>
/// <para>
/// Typed codes are forgiving: surrounding whitespace, inner spaces and dashes are dropped and the case is folded,
/// so "k7q 2x9" and "K7Q-2X9" both read as <c>K7Q2X9</c>. The alphabet already excludes 0/O/1/I/L.
/// </para>
/// </remarks>
public static class ShowcaseCodeParser
{
    public const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";
    public const int Length = 6;

    private const string GoPath = "/api/v1/go/";

    /// <summary>A scanned QR payload: only a <c>/go/{code}</c> URL is an AgendaMe code.</summary>
    public static bool TryParseScan(string? payload, out string code)
    {
        code = string.Empty;
        if (string.IsNullOrWhiteSpace(payload))
            return false;

        return TryParseUrl(payload.Trim(), out code);
    }

    /// <summary>Typed text: a bare code, or a pasted <c>/go/{code}</c> URL.</summary>
    public static bool TryParse(string? input, out string code)
    {
        code = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var trimmed = input.Trim();
        if (trimmed.Contains("://", StringComparison.Ordinal))
            return TryParseUrl(trimmed, out code);

        var normalised = Normalise(trimmed);
        if (!IsValid(normalised))
            return false;

        code = normalised;
        return true;
    }

    /// <summary>Trim, drop spaces and dashes, uppercase. Makes no claim that the result is valid.</summary>
    public static string Normalise(string? input) =>
        string.IsNullOrEmpty(input)
            ? string.Empty
            : new string(input.Trim()
                .Where(c => !char.IsWhiteSpace(c) && c != '-' && c != '‐' && c != '‑' && c != '–')
                .Select(char.ToUpperInvariant)
                .ToArray());

    public static bool IsValid(string? code) =>
        code is { Length: Length } && code.All(c => Alphabet.Contains(c));

    /// <summary>Printed in two groups of three, e.g. "K7Q 2X9", which is easier to read aloud and to type.</summary>
    public static string FormatForDisplay(string? code)
    {
        var normalised = Normalise(code);
        return normalised.Length == Length ? $"{normalised[..3]} {normalised[3..]}" : normalised;
    }

    private static bool TryParseUrl(string text, out string code)
    {
        code = string.Empty;
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri))
            return false;

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return false;

        var path = uri.AbsolutePath.TrimEnd('/');
        var index = path.LastIndexOf(GoPath, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            return false;

        var candidate = path[(index + GoPath.Length)..];
        if (candidate.Contains('/'))
            return false;

        var normalised = Normalise(Uri.UnescapeDataString(candidate));
        if (!IsValid(normalised))
            return false;

        code = normalised;
        return true;
    }
}
