using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Library.Entities;
using MobileApp.Models;
using MobileApp.Services;

namespace MobileApp.ViewModels;

public partial class NotificationsViewModel : ObservableObject
{
    private readonly INotificationApiService _notificationApiService;
    private readonly IUserSessionService _session;

    [ObservableProperty]
    private List<NotificationSummary> _notifications = new();

    [ObservableProperty]
    private int _unreadCount;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool IsEmpty => !IsLoading && Notifications.Count == 0 && !HasError;

    /// <summary>
    /// ux-review.md finding 1 / PRD Requirement 12 / AC13: an empty list is a normal state, not an
    /// error — acknowledgement + value prop, no action pathway since there is nothing to do from empty.
    /// </summary>
    public string EmptyStateMessage => "No notifications yet — you'll see updates about your appointments here.";

    public NotificationsViewModel(INotificationApiService notificationApiService, IUserSessionService session)
    {
        _notificationApiService = notificationApiService;
        _session = session;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;
        await _session.RefreshAsync();

        try
        {
            var results = await _notificationApiService.GetNotificationsAsync();
            Notifications = results;
            UnreadCount = Notifications.Count(n => !n.IsRead);
        }
        catch (Exception)
        {
            // Real failure (network, timeout, malformed response, ambiguous write, etc.) — surface it
            // through the error banner rather than masking it with fabricated data (F-015-T08, AC8).
            ErrorMessage = "Could not load notifications. Check your connection and try again.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task MarkReadAsync(string id)
    {
        var updated = await _notificationApiService.MarkReadAsync(id);
        if (updated is null)
            return;

        var index = Notifications.FindIndex(n => n.Id == id);
        if (index < 0)
            return;

        if (!Notifications[index].IsRead)
        {
            Notifications[index].IsRead = true;
            // Replace the list reference so CollectionView picks up the change
            Notifications = new List<NotificationSummary>(Notifications);
            UnreadCount = Math.Max(0, UnreadCount - 1);
        }
    }

    [RelayCommand]
    private void ToggleNotification(NotificationSummary notification)
    {
        notification.IsExpanded = !notification.IsExpanded;

        if (notification.IsExpanded && !notification.IsRead)
            ScheduleMarkRead(notification);
    }

    private async void ScheduleMarkRead(NotificationSummary notification)
    {
        await Task.Delay(2000);
        if (!notification.IsExpanded || notification.IsRead)
            return;

        notification.IsRead = true;
        Notifications = new List<NotificationSummary>(Notifications);
        UnreadCount = Math.Max(0, UnreadCount - 1);
    }

    partial void OnErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(IsEmpty));
    }

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsEmpty));

    partial void OnNotificationsChanged(List<NotificationSummary> value) => OnPropertyChanged(nameof(IsEmpty));
}
