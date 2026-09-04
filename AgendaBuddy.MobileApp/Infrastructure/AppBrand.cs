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
}
