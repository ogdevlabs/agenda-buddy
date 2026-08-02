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
            if (results.Count == 0)
                results = GenerateSeedNotifications();
            Notifications = results;
            UnreadCount = Notifications.Count(n => !n.IsRead);
        }
        catch (HttpRequestException)
        {
            Notifications = GenerateSeedNotifications();
            UnreadCount = Notifications.Count(n => !n.IsRead);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private List<NotificationSummary> GenerateSeedNotifications()
    {
        var now = DateTime.Now;

        if (_session.IsCustomer)
        {
            return
            [
                new NotificationSummary
                {
                    Id = "notif-c1",
                    NotificationType = NotificationType.AppointmentBooked,
                    Message = "Your session with Sarah Mitchell is confirmed for tomorrow at 9:00 AM",
                    CreatedAt = now.AddMinutes(-30),
                    IsRead = false
                },
                new NotificationSummary
                {
                    Id = "notif-c2",
                    NotificationType = NotificationType.AppointmentUpdated,
                    Message = "Sarah Mitchell rescheduled your Friday session to 2:30 PM",
                    CreatedAt = now.AddHours(-4),
                    IsRead = true
                }
            ];
        }

        return
        [
            new NotificationSummary
            {
                Id = "notif-1",
                NotificationType = NotificationType.AppointmentBooked,
                Message = "Alex Chen booked a session for tomorrow at 9:00 AM",
                CreatedAt = now.AddMinutes(-15),
                IsRead = false
            },
            new NotificationSummary
            {
                Id = "notif-2",
                NotificationType = NotificationType.AppointmentUpdated,
                Message = "Priya Sharma rescheduled Friday's session to 2:30 PM",
                CreatedAt = now.AddHours(-2),
                IsRead = false
            },
            new NotificationSummary
            {
                Id = "notif-3",
                NotificationType = NotificationType.AppointmentCompleted,
                Message = "Session with David Thompson marked as completed",
                CreatedAt = now.AddHours(-5),
                IsRead = true
            },
            new NotificationSummary
            {
                Id = "notif-4",
                NotificationType = NotificationType.AppointmentCancelled,
                Message = "Alex Chen cancelled next Monday's 3:00 PM session",
                CreatedAt = now.AddDays(-1),
                IsRead = true
            },
            new NotificationSummary
            {
                Id = "notif-5",
                NotificationType = NotificationType.AppointmentBooked,
                Message = "David Thompson booked a new session for Wednesday at 1:00 PM",
                CreatedAt = now.AddDays(-1).AddHours(-4),
                IsRead = true
            }
        ];
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
