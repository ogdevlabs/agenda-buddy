using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileApp.Models;
using MobileApp.Services;

namespace MobileApp.ViewModels;

public partial class NotificationsViewModel : ObservableObject
{
    private readonly INotificationApiService _notificationApiService;

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

    public NotificationsViewModel(INotificationApiService notificationApiService)
    {
        _notificationApiService = notificationApiService;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            var results = await _notificationApiService.GetNotificationsAsync();
            Notifications = results;
            UnreadCount = Notifications.Count(n => !n.IsRead);
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Could not load notifications — check your connection and try again.";
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

    partial void OnErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(IsEmpty));
    }

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsEmpty));

    partial void OnNotificationsChanged(List<NotificationSummary> value) => OnPropertyChanged(nameof(IsEmpty));
}
