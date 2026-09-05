using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Services;

namespace AgendaBuddy.MobileApp.ViewModels;

public partial class NotificationsViewModel : ObservableObject
{
    private readonly INotificationApiService _notificationApiService;
    private readonly IUserSessionService _session;
    private readonly NotificationBadgeViewModel _badge;

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

    [ObservableProperty]
    private bool _showUnreadOnly;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool IsEmpty => !IsLoading && Notifications.Count == 0 && !HasError;

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

    /// <summary>Label for the filter toggle, naming what tapping it will do rather than the state it is in.</summary>
    public string UnreadFilterLabel => ShowUnreadOnly ? "Show all" : "Unread only";

    public NotificationsViewModel(
        INotificationApiService notificationApiService,
        IUserSessionService session,
        NotificationBadgeViewModel badge)
    {
        _notificationApiService = notificationApiService;
        _session = session;
        _badge = badge;
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
            Notifications = results;

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
        }
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

        ApplyRead(notification);
    }

    [RelayCommand]
    private async Task MarkAllReadAsync()
    {
        if (UnreadCount == 0) return;

        var marked = await _notificationApiService.MarkAllReadAsync();
        if (marked == 0) return;

        foreach (var notification in Notifications)
            notification.IsRead = true;

        UnreadCount = 0;
        _badge.Set(0);

        // With the unread filter on, everything just left the filtered set.
        if (ShowUnreadOnly)
            await LoadAsync();
    }

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
    }

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
    }
}
