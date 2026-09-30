using System.Collections.ObjectModel;
using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgendaBuddy.MobileApp.ViewModels;

public enum PortfolioTileAction
{
    MakeCover,
    MoveEarlier,
    MoveLater,
    EditCaption,
    Remove
}

/// <summary>
/// The provider's portfolio: add up to the capacity, reorder, caption, remove. Each image uploads on its own
/// tile so one failure costs one photo, not the batch, and a failed tile retries from the bytes it kept.
/// </summary>
/// <remarks>
/// Remove is not confirmed — it offers Undo for a few seconds instead, because a confirm on every removal teaches
/// people to tap through it. Undo re-adds the same hash and caption and puts it back where it was; the stored
/// image is content-addressed, so nothing has to be uploaded again.
/// </remarks>
public partial class PortfolioEditorViewModel : ObservableObject
{
    public static readonly TimeSpan UndoWindow = TimeSpan.FromSeconds(5);

    private readonly IShowcaseApiService _api;
    private readonly IImagePicker _picker;
    private readonly IInAppAlertService _alerts;

    [ObservableProperty]
    private string _providerRef = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _errorMessage = string.Empty;

    public PortfolioEditorViewModel(IShowcaseApiService api, IImagePicker picker, IInAppAlertService alerts)
    {
        _api = api;
        _picker = picker;
        _alerts = alerts;
        Tiles.CollectionChanged += (_, _) => OnTilesChanged();
    }

    /// <summary>Raised when a tile's ⋯ is tapped; the page shows the action sheet and calls <see cref="ApplyAsync"/>.</summary>
    public event EventHandler<PortfolioTile>? MenuRequested;

    public ObservableCollection<PortfolioTile> Tiles { get; } = new();

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public int StoredCount => Tiles.Count(t => t.HasImage);
    public string CountLabel => ShowcaseText.PortfolioCount(Tiles.Count);
    public int RemainingSlots => ShowcaseText.RemainingSlots(Tiles.Count);
    public bool CanAdd => RemainingSlots > 0;
    public bool IsFull => !CanAdd;
    public bool IsEmpty => Tiles.Count == 0 && !IsLoading;

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            var result = await _api.GetMineAsync();
            if (result.IsSuccess && result.Value is not null)
                Apply(result.Value);
            else
                ErrorMessage = ShowcaseErrorCopy.Describe(result);
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        ErrorMessage = string.Empty;
        var limit = RemainingSlots;
        if (limit <= 0)
        {
            ErrorMessage = ShowcaseErrorCopy.For(ShowcaseErrorCodes.PortfolioFull);
            return;
        }

        IReadOnlyList<byte[]> picked;
        try
        {
            picked = await _picker.PickAsync(limit);
        }
        catch (Exception)
        {
            ErrorMessage = AppResources.GetString("Showcase_PickFailed");
            return;
        }

        var pending = new List<PortfolioTile>();
        foreach (var jpeg in picked.Take(limit))
        {
            var tile = new PortfolioTile
            {
                ProviderRef = ProviderRef,
                PendingJpeg = jpeg,
                State = PortfolioTileState.Uploading
            };
            Tiles.Add(tile);
            pending.Add(tile);
        }

        Renumber();
        foreach (var tile in pending)
            await UploadAsync(tile);
    }

    [RelayCommand]
    private Task RetryAsync(PortfolioTile? tile) =>
        tile is { IsFailed: true, PendingJpeg: not null } ? UploadAsync(tile) : Task.CompletedTask;

    [RelayCommand]
    private void Discard(PortfolioTile? tile)
    {
        if (tile is null || tile.HasImage)
            return;

        Tiles.Remove(tile);
        Renumber();
    }

    [RelayCommand]
    private void OpenMenu(PortfolioTile? tile)
    {
        if (tile is { IsReady: true, HasImage: true })
            MenuRequested?.Invoke(this, tile);
    }

    /// <summary>The actions offered for a tile, in menu order. The first cannot move earlier, the last later.</summary>
    public IReadOnlyList<PortfolioTileAction> ActionsFor(PortfolioTile tile)
    {
        var actions = new List<PortfolioTileAction>();
        var index = Tiles.IndexOf(tile);
        if (index > 0)
        {
            actions.Add(PortfolioTileAction.MakeCover);
            actions.Add(PortfolioTileAction.MoveEarlier);
        }

        if (index >= 0 && index < Tiles.Count - 1)
            actions.Add(PortfolioTileAction.MoveLater);

        actions.Add(PortfolioTileAction.EditCaption);
        actions.Add(PortfolioTileAction.Remove);
        return actions;
    }

    public static string LabelFor(PortfolioTileAction action) => AppResources.GetString(action switch
    {
        PortfolioTileAction.MakeCover => "Portfolio_MakeCover",
        PortfolioTileAction.MoveEarlier => "Portfolio_MoveEarlier",
        PortfolioTileAction.MoveLater => "Portfolio_MoveLater",
        PortfolioTileAction.EditCaption => "Portfolio_EditCaption",
        _ => "Portfolio_Remove"
    });

    public Task ApplyAsync(PortfolioTile tile, PortfolioTileAction action) => action switch
    {
        PortfolioTileAction.MakeCover => MoveToAsync(tile, 0),
        PortfolioTileAction.MoveEarlier => MoveToAsync(tile, Tiles.IndexOf(tile) - 1),
        PortfolioTileAction.MoveLater => MoveToAsync(tile, Tiles.IndexOf(tile) + 1),
        PortfolioTileAction.Remove => RemoveAsync(tile),
        _ => Task.CompletedTask
    };

    /// <summary>Saves a caption. Returns false, with the reason in <see cref="ErrorMessage"/>, when it was not saved.</summary>
    public async Task<bool> SaveCaptionAsync(PortfolioTile tile, string? caption)
    {
        ErrorMessage = string.Empty;
        var trimmed = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim();
        if (ShowcaseText.VisibleLength(trimmed) > ShowcaseText.CaptionMaxLength)
        {
            ErrorMessage = AppResources.Format("Showcase_CaptionTooLong", ShowcaseText.CaptionMaxLength);
            return false;
        }

        IsBusy = true;
        try
        {
            var result = await _api.UpdatePortfolioItemAsync(tile.Hash, trimmed, tile.ServiceId);
            if (!result.IsSuccess)
            {
                ErrorMessage = ShowcaseErrorCopy.Describe(result);
                return false;
            }

            tile.Caption = result.Value?.Caption ?? trimmed;
            return true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task MoveToAsync(PortfolioTile tile, int target)
    {
        var from = Tiles.IndexOf(tile);
        if (from < 0 || target < 0 || target >= Tiles.Count || target == from)
            return;

        ErrorMessage = string.Empty;
        var order = Tiles.Where(t => t.HasImage).Select(t => t.Hash).ToList();
        order.Remove(tile.Hash);
        order.Insert(Math.Min(target, order.Count), tile.Hash);
        await ReorderAsync(order);
    }

    public async Task RemoveAsync(PortfolioTile tile)
    {
        var index = Tiles.IndexOf(tile);
        if (index < 0 || !tile.HasImage)
            return;

        ErrorMessage = string.Empty;
        IsBusy = true;
        try
        {
            var result = await _api.RemovePortfolioItemAsync(tile.Hash);
            if (!result.IsSuccess)
            {
                ErrorMessage = ShowcaseErrorCopy.Describe(result);
                return;
            }
        }
        finally
        {
            IsBusy = false;
        }

        var previousOrder = Tiles.Where(t => t.HasImage).Select(t => t.Hash).ToList();
        Tiles.Remove(tile);
        Renumber();

        await _alerts.ShowAsync(
            AppResources.GetString("Portfolio_Removed"),
            AppResources.GetString("General_Undo"),
            () => UndoRemoveAsync(tile, previousOrder),
            UndoWindow);
    }

    public async Task UndoRemoveAsync(PortfolioTile tile, IReadOnlyList<string> previousOrder)
    {
        ErrorMessage = string.Empty;
        var added = await _api.AddPortfolioItemAsync(tile.Hash, tile.Caption, tile.ServiceId);
        if (!added.IsSuccess)
        {
            ErrorMessage = ShowcaseErrorCopy.Describe(added);
            return;
        }

        var current = Tiles.Where(t => t.HasImage).Select(t => t.Hash).ToHashSet(StringComparer.Ordinal);
        current.Add(tile.Hash);
        var order = previousOrder.Where(current.Contains).ToList();
        order.AddRange(current.Where(h => !order.Contains(h, StringComparer.Ordinal)));

        var restoredAtEnd = order.Count > 0 && order[^1] == tile.Hash;
        if (restoredAtEnd)
        {
            await LoadAsync();
            return;
        }

        await ReorderAsync(order);
    }

    private async Task UploadAsync(PortfolioTile tile)
    {
        if (tile.PendingJpeg is null)
            return;

        tile.State = PortfolioTileState.Uploading;
        tile.ErrorText = string.Empty;

        var upload = await _api.UploadImageAsync(tile.PendingJpeg);
        if (!upload.IsSuccess || upload.Value is null)
        {
            Fail(tile, ShowcaseErrorCopy.Describe(upload));
            return;
        }

        var added = await _api.AddPortfolioItemAsync(upload.Value.Hash, null, null);
        if (!added.IsSuccess)
        {
            Fail(tile, ShowcaseErrorCopy.Describe(added));
            return;
        }

        tile.Hash = upload.Value.Hash;
        tile.PendingJpeg = null;
        tile.State = PortfolioTileState.Ready;
        OnTilesChanged();
    }

    private static void Fail(PortfolioTile tile, string message)
    {
        tile.ErrorText = message;
        tile.State = PortfolioTileState.Failed;
    }

    private async Task ReorderAsync(IReadOnlyList<string> order)
    {
        IsBusy = true;
        try
        {
            var result = await _api.ReorderPortfolioAsync(order);
            if (result.IsSuccess && result.Value is not null)
            {
                Apply(result.Value);
                return;
            }

            if (result.ErrorCode == ShowcaseErrorCodes.PortfolioChanged)
            {
                await LoadAsync();
                ErrorMessage = ShowcaseErrorCopy.For(ShowcaseErrorCodes.PortfolioChanged);
                return;
            }

            ErrorMessage = ShowcaseErrorCopy.Describe(result);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Replaces the stored tiles with the server's list, keeping any still uploading or failed at the end.</summary>
    public void Apply(MyShowcase showcase)
    {
        ProviderRef = showcase.ProviderRef;
        var inFlight = Tiles.Where(t => !t.HasImage).ToList();
        Tiles.Clear();
        foreach (var tile in PortfolioTile.From(showcase.ProviderRef, showcase.Portfolio))
            Tiles.Add(tile);
        foreach (var tile in inFlight)
            Tiles.Add(tile);
        Renumber();
    }

    private void Renumber()
    {
        for (var i = 0; i < Tiles.Count; i++)
        {
            Tiles[i].Position = i + 1;
            Tiles[i].Count = Tiles.Count;
        }
    }

    private void OnTilesChanged()
    {
        OnPropertyChanged(nameof(StoredCount));
        OnPropertyChanged(nameof(CountLabel));
        OnPropertyChanged(nameof(RemainingSlots));
        OnPropertyChanged(nameof(CanAdd));
        OnPropertyChanged(nameof(IsFull));
        OnPropertyChanged(nameof(IsEmpty));
    }
}
