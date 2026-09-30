using System.Security.Cryptography;

namespace AgendaBuddy.Library.Showcase;

/// <summary>
/// Six characters from an alphabet without 0/O/1/I/L, so a printed code survives being read aloud or retyped.
/// </summary>
public static class PublicCodeGenerator
{
    public const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";
    public const int Length = 6;

    public static string Next()
    {
        Span<char> code = stackalloc char[Length];
        for (var i = 0; i < Length; i++)
            code[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return new string(code);
    }

    /// <summary>Uppercases and strips spaces and dashes; <c>null</c> when what remains is not a well-formed code.</summary>
    public static string? Normalise(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        var cleaned = new string(input.Where(c => c is not (' ' or '-')).ToArray()).ToUpperInvariant();
        return cleaned.Length == Length && cleaned.All(Alphabet.Contains) ? cleaned : null;
    }
}
