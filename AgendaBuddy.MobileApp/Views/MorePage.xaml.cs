#if MOBILE
using AgendaBuddy.MobileApp.Services;
using AgendaBuddy.MobileApp.ViewModels;

namespace AgendaBuddy.MobileApp.Views;

public partial class MorePage : ContentPage
{
    private readonly NotificationBadgeViewModel _badge;
    private readonly IUserSessionService _session;

    public MorePage(NotificationBadgeViewModel badge, IUserSessionService session)
    {
        InitializeComponent();
        _badge = badge;
        _session = session;
        BindingContext = _badge;
    }

    /// <summary>
    /// Re-reads the unread count every time this page is shown. It is the only surface carrying the badge, and
    /// it is also the only route to Notifications, so a stale count here is a notification nobody is told about.
    /// </summary>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        HiddenProvidersRow.IsVisible = _session.IsCustomer;
        HiddenProvidersDivider.IsVisible = _session.IsCustomer;
        _ = _badge.RefreshAsync();
    }

    private async void OnNotificationsClicked(object? sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("notifications");
    }

    private async void OnProfileClicked(object? sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("profile");
    }

    private async void OnScanClicked(object? sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("scanProvider");
    }

    private async void OnHiddenProvidersClicked(object? sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("hiddenProviders");
    }
}
#endif
