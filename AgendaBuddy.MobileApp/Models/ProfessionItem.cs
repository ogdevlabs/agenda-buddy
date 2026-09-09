using CommunityToolkit.Mvvm.ComponentModel;
using AgendaBuddy.MobileApp.Resources.Strings;

namespace AgendaBuddy.MobileApp.Models;

/// <summary>One catalog entry, plus whether the current provider has selected it — see
/// <see cref="Routing.ProfessionRouteBuilder"/>'s remarks.</summary>
public partial class ProfessionItem : ObservableObject
{
    public string Name { get; set; } = string.Empty;

    public string DisplayName => ProfessionDisplayNames.Get(Name);

    [ObservableProperty]
    private bool _isSelected;
}
