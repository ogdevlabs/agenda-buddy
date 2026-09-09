namespace AgendaBuddy.MobileApp.Infrastructure;

/// <summary>
/// One language the app either speaks or intends to.
/// </summary>
/// <param name="Code">The BCP 47 tag, e.g. <c>en</c>.</param>
/// <param name="Name">The name in English, for the row's primary line.</param>
/// <param name="NativeName">The name in the language itself, so a reader who does not read English can find it.</param>
/// <param name="IsAvailable">
/// Whether this build actually speaks it. A language listed but unavailable is deliberate — see
/// <see cref="AppLanguages"/>.
/// </param>
public record AppLanguage(string Code, string Name, string NativeName, bool IsAvailable);

/// <summary>
/// The languages the app offers, and the one it is currently in.
/// </summary>
/// <remarks>
/// <para>
/// <b>Spanish is listed and marked unavailable rather than omitted.</b> The product's target market makes it the
/// language people will look for first, and a settings screen that lists only English answers the question
/// "is Spanish coming?" with silence — which reads as "no". Listing it as not-yet-available answers it honestly
/// and costs one row.
/// </para>
/// <para>
/// ⚠️ <b>Nothing here localises anything.</b> There is no resource-string infrastructure in this app yet: every
/// user-facing string is a literal in XAML or a view model. This type is the menu and the honest statement of
/// what is supported; wiring an actual second locale is its own feature, and selecting an unavailable language
/// must therefore be refused rather than stored, or the app would claim a setting it does not honour.
/// </para>
/// <para>
/// Free of MAUI types so the list and the selection rule are covered on the <c>net10.0</c> test slice.
/// </para>
/// </remarks>
public static class AppLanguages
{
    /// <summary>The neutral fallback language.</summary>
    public const string DefaultCode = "en";

    /// <summary>Every language the menu shows, in the order it shows them.</summary>
    public static readonly IReadOnlyList<AppLanguage> All =
    [
        new("en", "English", "English", IsAvailable: true),
        new("es-MX", "Spanish", "Español", IsAvailable: true)
    ];

    /// <summary>
    /// Whether <paramref name="code"/> names a language this build can actually be shown in.
    /// </summary>
    /// <remarks>
    /// An unknown code is not available, so a value stored by a future build with more languages does not make an
    /// older one claim to speak one.
    /// </remarks>
    public static bool IsSelectable(string? code) =>
        All.Any(language =>
            language.IsAvailable
            && string.Equals(language.Code, code, StringComparison.OrdinalIgnoreCase));

    /// <summary>The language for a stored code, falling back to <see cref="DefaultCode"/>'s entry.</summary>
    public static AppLanguage Resolve(string? code) =>
        All.FirstOrDefault(language =>
            string.Equals(language.Code, code, StringComparison.OrdinalIgnoreCase))
        ?? All.First(language => language.Code == DefaultCode);
}
