using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Services;

namespace AgendaBuddy.MobileApp.ViewModels;

public partial class NotificationsViewModel : ObservableObject
{
    private readonly INotificationApiService _notificationApiService;
    private readonly IUserSessionService _session;
    private readonly NotificationBadgeViewModel _badge;
    private readonly IInAppAlertService? _alerts;

    /// <summary>
    /// How long an expanded notification has to stay open before it counts as read. Long enough that a
    /// mis-tap does not silently clear something the reader never saw.
    /// </summary>
    private static readonly TimeSpan MarkReadDelay = TimeSpan.FromSeconds(2);

    /// <summary>
    /// How many rows to ask for.
    /// </summary>
    /// <remarks>
    /// Sent explicitly rather than left to the route's default. The screen is what knows how many rows it can
    /// usefully render, and an unstated page size is a coupling to a server constant the client cannot see —
    /// the same invisible-agreement shape that let the wire contract and the client model drift apart. The
    /// route clamps it (1–200), so this is a request, not a trusted bound.
    /// </remarks>
    public const int PageSize = 50;

    [ObservableProperty]
    private List<NotificationSummary> _notifications = new();

    [ObservableProperty]
    private int _unreadCount;

    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// Drives the pull-to-refresh spinner, separately from <see cref="IsLoading"/>.
    /// </summary>
    /// <remarks>
    /// Two flags because they mean different things to the view: a first load shows the centred activity
    /// indicator over an empty page, a pull-to-refresh shows the gesture's own spinner and must leave the rows
    /// the reader is looking at in place. Sharing one flag makes a refresh blank the list it is refreshing.
    /// </remarks>
    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private bool _showUnreadOnly;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool IsEmpty => !IsLoading && Notifications.Count == 0 && !HasError;

    /// <summary>
    /// Whether to draw the centred activity indicator. A pull-to-refresh draws its own spinner, so showing
    /// this one at the same time reads as two separate things loading.
    /// </summary>
    public bool ShowsLoadingIndicator => IsLoading && !IsRefreshing;

    /// <summary>
    /// An empty list is a normal state, not an error — acknowledgement plus what the surface is for. Worded
    /// for the filter that is on, so a filtered-empty inbox does not read as an empty one.
    /// </summary>
    public string EmptyStateMessage => ShowUnreadOnly
        ? "Nothing unread — you're all caught up."
        : "No notifications yet — you'll see updates about your appointments here.";

    /// <summary>
    /// Whether to show the unread count at all. A real <c>bool</c> rather than XAML binding the <c>int</c>
    /// straight at <c>IsVisible</c>, which is not a conversion MAUI defines — so the count label's visibility
    /// was not actually driven by whether there was anything unread.
    /// </summary>
    public bool HasUnread => UnreadCount > 0;

    /// <summary>Bulk mark-read is pointless with nothing unread, and a button that does nothing is worse than no button.</summary>
    public bool CanMarkAllRead => UnreadCount > 0 && !IsLoading;

    /// <summary>
    /// The unread count as a phrase, so the header reads as a sentence rather than as a number and a noun that
    /// disagree with it ("1 unread" is right, "1 unread notifications" is not).
    /// </summary>
    public string UnreadSummary => UnreadCount == 1 ? "1 unread" : $"{UnreadCount} unread";

    /// <summary>Label for the filter toggle, naming what tapping it will do rather than the state it is in.</summary>
    public string UnreadFilterLabel => ShowUnreadOnly ? "Show all" : "Unread only";

    public NotificationsViewModel(
        INotificationApiService notificationApiService,
        IUserSessionService session,
        NotificationBadgeViewModel badge,
        IInAppAlertService? alerts = null)
    {
        _notificationApiService = notificationApiService;
        _session = session;
        _badge = badge;
        _alerts = alerts;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;
        await _session.RefreshAsync();

        try
        {
            var results = await _notificationApiService.GetNotificationsAsync(PageSize, ShowUnreadOnly);
            Notifications = ApplySections(results);

            // The unread count comes from the server, not from counting this page: with a filter on or a
            // limit applied, the page is not the whole inbox, so counting it would under-report.
            await _badge.RefreshAsync();
            UnreadCount = (int)Math.Min(int.MaxValue, _badge.UnreadCount);
        }
        catch (Exception)
        {
            // Real failure (network, timeout, malformed response, a non-2xx from the route) — surface it
            // through the error banner rather than masking it with an empty inbox.
            ErrorMessage = "Could not load notifications. Check your connection and try again.";
        }
        finally
        {
            IsLoading = false;
            IsRefreshing = false;
        }
    }

    /// <summary>
    /// Pull-to-refresh. The same load, without blanking the rows already on screen.
    /// </summary>
    /// <remarks>
    /// Pull-to-refresh is the gesture people try first on any list they suspect is stale, and there was none
    /// here: the only way to re-read the inbox was to navigate away and back so <c>OnAppearing</c> fired.
    /// </remarks>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsRefreshing = true;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task ToggleUnreadFilterAsync()
    {
        ShowUnreadOnly = !ShowUnreadOnly;
        await LoadAsync();
    }

    /// <summary>
    /// Marks one notification read <b>on the server</b>, then locally. This order matters: the previous
    /// version updated only the local copy, so the next reload restored every row to unread and nothing in
    /// the app ever wrote <c>is_read</c>.
    /// </summary>
    [RelayCommand]
    private async Task MarkReadAsync(NotificationSummary? notification)
    {
        if (notification is null || notification.IsRead) return;

        if (!await _notificationApiService.MarkReadAsync(notification.Id))
            return;

        // Deliberately not reloading under the unread filter: the row has just been read *because the reader
        // opened it*, and dropping it out of the list while they are reading it is worse than briefly showing
        // a read row in an unread-only view. The next load files it correctly.
        ApplyRead(notification);
    }

    /// <summary>
    /// Marks the whole inbox read, and says so.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It reports its outcome, on all three paths. Before, it was silent on every one of them: a success looked
    /// identical to a no-op, and a server refusal or a dropped connection looked identical to both — a bulk
    /// action whose only feedback was a number that sometimes changed.
    /// </para>
    /// <para>
    /// The exception handler is not defensive padding: <c>MarkAllReadAsync</c> reaches the network, this is
    /// invoked as a command, and an unhandled exception out of an async command handler is unobserved rather
    /// than reported — the button would appear to do nothing at all.
    /// </para>
    /// </remarks>
    [RelayCommand]
    private async Task MarkAllReadAsync()
    {
        if (UnreadCount == 0) return;

        long? marked;
        try
        {
            marked = await _notificationApiService.MarkAllReadAsync();
        }
        catch (Exception)
        {
            marked = null;
        }

        if (marked is null)
        {
            // The request failed, so nothing local changes: clearing the rows would show a state the next
            // reload contradicts, and the reader would believe the inbox was cleared when it was not.
            ErrorMessage = MarkAllReadFailureMessage;
            return;
        }

        ErrorMessage = string.Empty;

        if (marked == 0)
        {
            // The server had nothing unread for this account — the local count was stale (read on another
            // device, most likely). Reconcile instead of reporting a failure that did not happen.
            await _badge.RefreshAsync();
            UnreadCount = (int)Math.Min(int.MaxValue, _badge.UnreadCount);

            if (_alerts is not null)
                await _alerts.ShowAsync(NothingToMarkMessage);

            return;
        }

        foreach (var notification in Notifications)
            notification.IsRead = true;

        UnreadCount = 0;
        _badge.Set(0);

        if (_alerts is not null)
            await _alerts.ShowAsync(MarkAllReadConfirmation(marked.Value));

        // With the unread filter on, everything just left the filtered set.
        if (ShowUnreadOnly)
            await LoadAsync();
    }

    /// <summary>What the bulk action reports back. Names the count, because "done" is not the same as "12 done".</summary>
    internal static string MarkAllReadConfirmation(long marked) =>
        marked == 1 ? "1 notification marked as read" : $"{marked} notifications marked as read";

    internal const string MarkAllReadFailureMessage =
        "Could not mark your notifications read. Check your connection and try again.";

    internal const string NothingToMarkMessage = "Nothing left to mark as read.";

    /// <summary>
    /// Expands or collapses a row. Expanding an unread one starts the read timer — reading is what marking
    /// read is supposed to mean, so it is not a separate button the reader has to find.
    /// </summary>
    [RelayCommand]
    private void ToggleNotification(NotificationSummary notification)
    {
        notification.IsExpanded = !notification.IsExpanded;

        if (notification.IsExpanded && !notification.IsRead)
            _ = ScheduleMarkReadAsync(notification);
    }

    /// <summary>
    /// Opens the appointment a notification is about. Without this a notification is a dead end: the
    /// identifier is on the row and the route already exists, so the only thing missing was the tap.
    /// </summary>
    [RelayCommand]
    private async Task ViewAppointmentAsync(NotificationSummary? notification)
    {
        // CanOpenAppointment, not HasAppointment: a cancellation names an appointment that has been deleted.
        // Checked here as well as bound to the button's visibility, because a hidden control is not a guard.
        if (notification is null || !notification.CanOpenAppointment) return;

        // Reading it is implied by acting on it, so the row does not stay unread behind an opened appointment.
        if (!notification.IsRead)
            await MarkReadAsync(notification);

        await NavigateToAppointmentAsync(notification.AppointmentIdentifier);
    }

    /// <summary>
    /// Overridable so the view model is testable under the <c>net10.0</c> test slice, where
    /// <c>Shell.Current</c> does not exist.
    /// </summary>
    protected virtual Task NavigateToAppointmentAsync(string appointmentIdentifier)
    {
#if MOBILE
        return AppShell.NavigateToAppointmentAsync(appointmentIdentifier);
#else
        return Task.CompletedTask;
#endif
    }

    /// <summary>
    /// Awaitable rather than <c>async void</c>: an exception from an <c>async void</c> cannot be caught by a
    /// caller and takes the process down instead of losing a mark-read.
    /// </summary>
    internal async Task ScheduleMarkReadAsync(NotificationSummary notification)
    {
        await Task.Delay(MarkReadDelay);

        // Collapsed again, or already read by another path, in the meantime.
        if (!notification.IsExpanded || notification.IsRead) return;

        await MarkReadAsync(notification);
    }

    /// <summary>
    /// Stamps the date band on the first row of each band and clears it on the rest, so the list can draw
    /// "Today"/"Yesterday" headers from a flat list.
    /// </summary>
    /// <remarks>
    /// A flat list with headers on the boundary rows rather than a grouped <c>CollectionView</c> — the same
    /// choice the professions catalog made, and for a related reason: grouping buys nothing here (nothing
    /// collapses) and costs a second template plus MAUI's own grouped-header rendering quirks.
    /// <para>
    /// Banded on <b>local</b> dates. The server stores UTC instants, and banding on those files a 01:00Z
    /// notification under the wrong day for every reader behind UTC — the same defect
    /// <c>ProviderAvailability</c> had.
    /// </para>
    /// </remarks>
    internal static List<NotificationSummary> ApplySections(List<NotificationSummary> rows)
    {
        var now = DateTime.Now;
        var previous = string.Empty;

        foreach (var row in rows)
        {
            var section = NotificationVisuals.Section(row.CreatedAt.ToLocalTime(), now);
            row.SectionHeader = section == previous ? string.Empty : section;
            previous = section;
        }

        return rows;
    }

    private void ApplyRead(NotificationSummary notification)
    {
        notification.IsRead = true;
        UnreadCount = Math.Max(0, UnreadCount - 1);
        _badge.Decrement();
    }

    partial void OnErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(IsEmpty));
    }

    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(CanMarkAllRead));
        OnPropertyChanged(nameof(ShowsLoadingIndicator));
    }

    partial void OnIsRefreshingChanged(bool value) => OnPropertyChanged(nameof(ShowsLoadingIndicator));

    partial void OnNotificationsChanged(List<NotificationSummary> value) => OnPropertyChanged(nameof(IsEmpty));

    partial void OnShowUnreadOnlyChanged(bool value)
    {
        OnPropertyChanged(nameof(EmptyStateMessage));
        OnPropertyChanged(nameof(UnreadFilterLabel));
    }

    partial void OnUnreadCountChanged(int value)
    {
        OnPropertyChanged(nameof(HasUnread));
        OnPropertyChanged(nameof(CanMarkAllRead));
        OnPropertyChanged(nameof(UnreadSummary));
    }
}
