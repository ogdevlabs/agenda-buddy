using System.Globalization;
using AgendaBuddy.MobileApp.Resources.Strings;

namespace AgendaBuddy.MobileApp.Infrastructure;

public interface ILanguagePreferenceStore
{
    string? Get();
    void Set(string languageCode);
}

public interface ILanguageCoordinator
{
    string CurrentCode { get; }
    void Initialize(CultureInfo deviceCulture);
    bool Select(string? languageCode);
}

public sealed class LanguageCoordinator : ILanguageCoordinator
{
    private readonly ILanguagePreferenceStore _store;
    private readonly Func<string?, bool> _isSelectable;

    public LanguageCoordinator(
        ILanguagePreferenceStore store,
        Func<string?, bool>? isSelectable = null)
    {
        _store = store;
        _isSelectable = isSelectable ?? AppLanguages.IsSelectable;
    }

    public string CurrentCode { get; private set; } = AppLanguages.DefaultCode;

    public void Initialize(CultureInfo deviceCulture)
    {
        ArgumentNullException.ThrowIfNull(deviceCulture);

        var persisted = Normalize(_store.Get());
        var device = Normalize(deviceCulture.Name);
        var selected = IsAllowed(persisted)
            ? persisted!
            : IsAllowed(device)
                ? device!
                : AppLanguages.DefaultCode;

        Apply(selected);
    }

    public bool Select(string? languageCode)
    {
        var normalized = Normalize(languageCode);
        if (!IsAllowed(normalized)) return false;

        var changed = !string.Equals(CurrentCode, normalized, StringComparison.OrdinalIgnoreCase);
        _store.Set(normalized!);
        Apply(normalized!);
        return changed;
    }

    private bool IsAllowed(string? languageCode) =>
        languageCode is not null && _isSelectable(languageCode);

    private static string? Normalize(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode)) return null;
        if (languageCode.StartsWith("es", StringComparison.OrdinalIgnoreCase)) return "es-MX";
        if (languageCode.StartsWith("en", StringComparison.OrdinalIgnoreCase)) return "en";
        return languageCode;
    }

    private void Apply(string languageCode)
    {
        var culture = CultureInfo.GetCultureInfo(languageCode);

        CurrentCode = languageCode;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        AppResources.Culture = culture;
    }
}