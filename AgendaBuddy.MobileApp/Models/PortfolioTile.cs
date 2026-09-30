using AgendaBuddy.MobileApp.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AgendaBuddy.MobileApp.Models;

public enum PortfolioTileState
{
    Ready,
    Uploading,
    Failed
}

/// <summary>One square in a portfolio grid: a stored image, or one still on its way up.</summary>
public partial class PortfolioTile : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasImage))]
    private string _hash = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Description))]
    private string? _caption;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Description), nameof(IsCover))]
    private int _position;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Description))]
    private int _count;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsUploading), nameof(IsFailed), nameof(IsReady))]
    private PortfolioTileState _state = PortfolioTileState.Ready;

    [ObservableProperty]
    private string _errorText = string.Empty;

    public string ProviderRef { get; init; } = string.Empty;

    public string? ServiceId { get; set; }

    /// <summary>Added since this customer last looked. Always false on the provider's own view.</summary>
    public bool IsNew { get; init; }

    /// <summary>The resized upload, kept so a failed tile can be retried without picking the photo again.</summary>
    public byte[]? PendingJpeg { get; set; }

    public bool HasImage => !string.IsNullOrEmpty(Hash);
    public bool IsUploading => State == PortfolioTileState.Uploading;
    public bool IsFailed => State == PortfolioTileState.Failed;
    public bool IsReady => State == PortfolioTileState.Ready;
    public bool IsCover => Position == 1;

    public string Description => ShowcaseText.TileDescription(Position, Count, Caption);

    public static List<PortfolioTile> From(string providerRef, IReadOnlyList<PortfolioItem> items)
    {
        var tiles = new List<PortfolioTile>(items.Count);
        for (var i = 0; i < items.Count; i++)
        {
            tiles.Add(new PortfolioTile
            {
                ProviderRef = providerRef,
                Hash = items[i].Hash,
                Caption = items[i].Caption,
                ServiceId = items[i].ServiceId,
                IsNew = items[i].IsNew,
                Position = i + 1,
                Count = items.Count
            });
        }

        return tiles;
    }
}
