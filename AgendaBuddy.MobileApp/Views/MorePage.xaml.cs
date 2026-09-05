#if MOBILE
namespace AgendaBuddy.MobileApp.Views;

public partial class MorePage : ContentPage
{
    public MorePage()
    {
        InitializeComponent();
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
