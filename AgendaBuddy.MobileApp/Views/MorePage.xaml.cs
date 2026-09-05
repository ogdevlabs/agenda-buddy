#if MOBILE
using AgendaBuddy.MobileApp.ViewModels;

namespace AgendaBuddy.MobileApp.Views;

public partial class MorePage : ContentPage
{
    private readonly NotificationBadgeViewModel _badge;

    public MorePage(NotificationBadgeViewModel badge)
    {
        InitializeComponent();
        _badge = badge;
        BindingContext = _badge;
    }

    /// <summary>
    /// Re-reads the unread count every time this page is shown. It is the only surface carrying the badge, and
    /// it is also the only route to Notifications, so a stale count here is a notification nobody is told about.
    /// </summary>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = _badge.RefreshAsync();
    }

    private async void OnNotificationsClicked(object? sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("notifications");
    }

    private async void OnAccountClicked(object? sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("account");
    }
}
#endif
