#if MOBILE
namespace AgendaBuddy.MobileApp.Infrastructure;

public sealed class MauiLanguagePreferenceStore : ILanguagePreferenceStore
{
    private const string PreferenceKey = "app_language";

    public string? Get() => Preferences.Default.Get<string?>(PreferenceKey, null);

    public void Set(string languageCode) => Preferences.Default.Set(PreferenceKey, languageCode);
}
#endif