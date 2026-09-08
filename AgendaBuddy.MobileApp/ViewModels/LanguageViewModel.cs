using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgendaBuddy.MobileApp.Infrastructure;

namespace AgendaBuddy.MobileApp.ViewModels;

/// <summary>
/// One row on the language screen.
/// </summary>
public partial class LanguageOption : ObservableObject
{
    public required AppLanguage Language { get; init; }

    public string Code => Language.Code;
    public string Name => Language.Name;
    public string NativeName => Language.NativeName;
    public bool IsAvailable => Language.IsAvailable;

    /// <summary>The row's secondary line — either the language's own name, or why it cannot be picked.</summary>
    public string Detail => IsAvailable ? NativeName : $"{NativeName} — coming soon";

    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>
/// The app's language.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>This screen currently reports a language rather than changing one.</b> There is no resource-string
/// infrastructure in this app — every user-facing string is a literal in XAML or a view model — so selecting
/// Spanish cannot be honoured, and storing the preference would make the app claim a setting it ignores. Tapping
/// it says so instead.
/// </para>
/// <para>
/// Spanish is listed and marked unavailable rather than omitted: it is the language this product's market will
/// look for first, and a screen listing only English answers "is Spanish coming?" with silence, which reads as no.
/// </para>
/// </remarks>
public partial class LanguageViewModel : ObservableObject
{
    public ObservableCollection<LanguageOption> Languages { get; } = [];

    [ObservableProperty]
    private string _selectedCode = AppLanguages.DefaultCode;

    /// <summary>The explanatory line under the list, shown always — the limitation is the screen's main fact.</summary>
    public string Notice =>
        "The app is currently available in English only. Spanish is planned; when it ships you will be able to "
        + "switch here.";

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

    /// <summary>
    /// Picks a language, or explains why it cannot be picked.
    /// </summary>
    /// <remarks>
    /// An unavailable option is <b>refused rather than stored</b>. Recording a preference nothing honours is worse
    /// than refusing it: the user would see Spanish ticked and an English app, and conclude the app is broken
    /// rather than that the language is not ready.
    /// </remarks>
    [RelayCommand]
    private async Task SelectAsync(LanguageOption? option)
    {
        if (option is null) return;

        if (!AppLanguages.IsSelectable(option.Code))
        {
            await ToastNotifier.ShowAsync($"{option.Name} is not available yet.");
            return;
        }

        SelectedCode = option.Code;
        ApplySelection();
    }

    private void ApplySelection()
    {
        foreach (var option in Languages)
            option.IsSelected = string.Equals(option.Code, SelectedCode, StringComparison.OrdinalIgnoreCase);
    }
}
