using AgendaBuddy.MobileApp.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AgendaBuddy.MobileApp.ViewModels;

/// <summary>Full-screen, swipeable portfolio images, opened on the one that was tapped.</summary>
public partial class PortfolioViewerViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PositionLabel), nameof(Caption), nameof(HasCaption))]
    private int _position;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PositionLabel), nameof(Caption), nameof(HasCaption))]
    private IReadOnlyList<PortfolioTile> _tiles = [];

    public string PositionLabel => Tiles.Count == 0
        ? string.Empty
        : AppResources.Format("Showcase_PortfolioCount", Current + 1, Tiles.Count);

    public string Caption => Tiles.Count == 0 ? string.Empty : Tiles[Current].Caption ?? string.Empty;

    public bool HasCaption => !string.IsNullOrWhiteSpace(Caption);

    private int Current => Math.Clamp(Position, 0, Math.Max(0, Tiles.Count - 1));

    public void Open(IReadOnlyList<PortfolioTile> tiles, int index)
    {
        Tiles = tiles;
        Position = tiles.Count == 0 ? 0 : Math.Clamp(index, 0, tiles.Count - 1);
    }
}
