using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgendaBuddy.MobileApp.ViewModels;

/// <summary>
/// Subscribes the phone's own calendar to the account's AgendaMe feed.
/// </summary>
/// <remarks>
/// The server sends the feed's link once, when it is issued, and stores only a hash of it — so this device keeps the
/// link in secure storage, keyed by account. A feed that is on with no link here (issued on another device, or before
/// a reinstall) cannot be shown again; the only way forward is a new link, which revokes the old one, so that path
/// asks first.
/// </remarks>
public partial class CalendarSyncViewModel : ObservableObject
{
    private const string LinkKeyPrefix = "calendar_feed_url:";

    private readonly ICalendarFeedApiService _api;
    private readonly ISecureStorageService _storage;
    private readonly IUserSessionService _session;
    private readonly ICalendarSyncDevice _device;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOff), nameof(IsOnWithLink), nameof(IsOnWithoutLink))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOff), nameof(IsOnWithLink), nameof(IsOnWithoutLink))]
    private bool _isLoaded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOff), nameof(IsOnWithLink), nameof(IsOnWithoutLink))]
    private bool _isEnabled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOnWithLink), nameof(IsOnWithoutLink))]
    private string _feedUrl = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    private string _statusMessage = string.Empty;

    public CalendarSyncViewModel(
        ICalendarFeedApiService api,
        ISecureStorageService storage,
        IUserSessionService session,
        ICalendarSyncDevice device)
    {
        _api = api;
        _storage = storage;
        _session = session;
        _device = device;
    }

    public bool IsOff => IsLoaded && !IsLoading && !IsEnabled;
    public bool IsOnWithLink => IsLoaded && !IsLoading && IsEnabled && !string.IsNullOrEmpty(FeedUrl);
    public bool IsOnWithoutLink => IsLoaded && !IsLoading && IsEnabled && string.IsNullOrEmpty(FeedUrl);
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool HasStatus => !string.IsNullOrEmpty(StatusMessage);

    /// <summary>The native calendar's action leads; the other stays one tap away for anyone who uses both.</summary>
    public string PrimarySubscribeLabel => AppResources.GetString(
        _device.PrefersAppleCalendar ? "CalendarSync_AddToApple" : "CalendarSync_AddToGoogle");

    public string SecondarySubscribeLabel => AppResources.GetString(
        _device.PrefersAppleCalendar ? "CalendarSync_AddToGoogle" : "CalendarSync_AddToApple");

    private string LinkKey => LinkKeyPrefix + _session.Email.Trim().ToLowerInvariant();

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;
        try
        {
            var status = await _api.GetStatusAsync();
            if (status is null)
            {
                ErrorMessage = AppResources.GetString("CalendarSync_LoadFailed");
                return;
            }

            IsEnabled = status.Enabled;
            if (status.Enabled)
            {
                FeedUrl = await _storage.GetAsync(LinkKey) ?? string.Empty;
            }
            else
            {
                // A stored link for a feed that is off is dead; offering it would subscribe a calendar to a 404.
                _storage.Remove(LinkKey);
                FeedUrl = string.Empty;
            }

            IsLoaded = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private Task TurnOnAsync() => IssueLinkAsync();

    /// <summary>Replaces the link. Every calendar subscribed with the old one stops updating, so it asks first.</summary>
    [RelayCommand]
    private async Task ResetLinkAsync()
    {
        var confirmed = await _device.ConfirmAsync(
            AppResources.GetString("CalendarSync_ResetConfirmTitle"),
            AppResources.GetString("CalendarSync_ResetConfirmMessage"),
            AppResources.GetString("CalendarSync_ResetConfirmAccept"),
            AppResources.GetString("General_Cancel"));
        if (confirmed)
            await IssueLinkAsync();
    }

    [RelayCommand]
    private async Task TurnOffAsync()
    {
        if (IsBusy)
            return;

        var confirmed = await _device.ConfirmAsync(
            AppResources.GetString("CalendarSync_TurnOffConfirmTitle"),
            AppResources.GetString("CalendarSync_TurnOffConfirmMessage"),
            AppResources.GetString("CalendarSync_TurnOff"),
            AppResources.GetString("General_Cancel"));
        if (!confirmed)
            return;

        IsBusy = true;
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;
        try
        {
            if (!await _api.DisableAsync())
            {
                ErrorMessage = AppResources.GetString("CalendarSync_ActionFailed");
                return;
            }

            _storage.Remove(LinkKey);
            FeedUrl = string.Empty;
            IsEnabled = false;
            IsLoaded = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task SubscribePrimaryAsync() => SubscribeAsync(_device.PrefersAppleCalendar);

    [RelayCommand]
    private Task SubscribeSecondaryAsync() => SubscribeAsync(!_device.PrefersAppleCalendar);

    public async Task SubscribeAsync(bool apple)
    {
        if (string.IsNullOrEmpty(FeedUrl))
            return;

        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;
        var target = apple ? CalendarSubscriptionLinks.Webcal(FeedUrl) : CalendarSubscriptionLinks.Google(FeedUrl);
        if (!await _device.OpenAsync(target))
            ErrorMessage = AppResources.GetString("CalendarSync_OpenFailed");
    }

    /// <summary>The plain https link, which is what any other calendar app's "subscribe by URL" field accepts.</summary>
    [RelayCommand]
    private async Task CopyLinkAsync()
    {
        if (string.IsNullOrEmpty(FeedUrl))
            return;

        ErrorMessage = string.Empty;
        try
        {
            await _device.CopyAsync(FeedUrl);
            StatusMessage = AppResources.GetString("CalendarSync_LinkCopied");
        }
        catch (Exception)
        {
            StatusMessage = string.Empty;
            ErrorMessage = AppResources.GetString("CalendarSync_CopyFailed");
        }
    }

    private async Task IssueLinkAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;
        try
        {
            var link = await _api.EnableAsync(AppResources.CurrentCulture.Name);
            if (link is null)
            {
                ErrorMessage = AppResources.GetString("CalendarSync_ActionFailed");
                return;
            }

            await _storage.SetAsync(LinkKey, link.Url);
            FeedUrl = link.Url;
            IsEnabled = true;
            IsLoaded = true;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
