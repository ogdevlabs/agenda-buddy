namespace AgendaBuddy.MobileApp.Infrastructure;

/// <summary>
/// The single definition of the app's user-visible name.
/// </summary>
/// <remarks>
/// Referenced from XAML via <c>x:Static</c> rather than copied into each view, so the brand cannot end up
/// spelled two ways across the app. Deliberately outside <c>#if MOBILE</c> so the net10.0 test slice can
/// assert against it.
/// </remarks>
public static class AppBrand
{
    /// <summary>Camel-cased: <c>AgendaMe</c>, not <c>Agendame</c>.</summary>
    public const string Name = "AgendaMe";

    /// <summary>
    /// The first half of the wordmark, set in the primary colour.
    /// </summary>
    /// <remarks>
    /// The wordmark is drawn in two tones so the <see cref="NameAccent"/> half carries the name's meaning
    /// rather than disappearing into one flat string. Split here rather than in XAML so the two halves
    /// cannot drift apart from <see cref="Name"/>.
    /// </remarks>
    public const string NameStem = "Agenda";

    /// <summary>The accented half of the wordmark.</summary>
    public const string NameAccent = "Me";

    /// <summary>
    /// The initials shown in the square mark on the auth screens, where the wordmark is already spelled out
    /// beside it.
    /// </summary>
    public const string Monogram = "AM";
}
