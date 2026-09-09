using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Resources.Strings;
using AgendaBuddy.MobileApp.Services;

namespace AgendaBuddy.MobileApp.ViewModels;

/// <summary>
/// One row on the language screen.
/// </summary>
public partial class LanguageOption : ObservableObject
{
    public required AppLanguage Language { get; init; }

    public string Code => Language.Code;
    public string Name => Code == AppLanguages.DefaultCode
        ? AppResources.Language_EnglishName
        : AppResources.Language_SpanishName;
    public string NativeName => Language.NativeName;
    public bool IsAvailable => Language.IsAvailable;

    /// <summary>The language's own name, so a reader can find it regardless of the current app language.</summary>
    public string Detail => NativeName;

    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>The app's language.</summary>
public partial class LanguageViewModel : ObservableObject
{
    private readonly ILanguageCoordinator? _languageCoordinator;
    private readonly ILanguageShellService? _languageShell;

    public LanguageViewModel(
        ILanguageCoordinator? languageCoordinator = null,
        ILanguageShellService? languageShell = null)
    {
        _languageCoordinator = languageCoordinator;
        _languageShell = languageShell;
        _selectedCode = languageCoordinator?.CurrentCode ?? AppLanguages.DefaultCode;
    }

    public ObservableCollection<LanguageOption> Languages { get; } = [];

    [ObservableProperty]
    private string _selectedCode = AppLanguages.DefaultCode;

    public string Notice => AppResources.GetString("Language_Notice");

    [RelayCommand]
    private void Load()
    {
        if (Languages.Count > 0)
        {
            ApplySelection();
            return;
        }

        foreach (var language in AppLanguages.All)
            Languages.Add(new LanguageOption { Language = language });

        ApplySelection();
    }

    /// <summary>Picks and persists one of the languages this build supports.</summary>
    [RelayCommand]
    private async Task SelectAsync(LanguageOption? option)
    {
        if (option is null) return;

        if (!AppLanguages.IsSelectable(option.Code))
        {
            await ToastNotifier.ShowAsync(AppResources.Format("Language_NotAvailable", option.Name));
            return;
        }

        var changed = _languageCoordinator?.Select(option.Code) ?? true;
        SelectedCode = _languageCoordinator?.CurrentCode ?? option.Code;
        ApplySelection();

        if (changed && _languageShell is not null)
            await _languageShell.RebuildAsync();
    }

    private void ApplySelection()
    {
        foreach (var option in Languages)
            option.IsSelected = string.Equals(option.Code, SelectedCode, StringComparison.OrdinalIgnoreCase);
    }
}
