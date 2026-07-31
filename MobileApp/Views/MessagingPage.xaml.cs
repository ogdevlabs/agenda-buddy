#if MOBILE
using MobileApp.Infrastructure;
using MobileApp.Models;
using MobileApp.ViewModels;

namespace MobileApp.Views;

public partial class MessagingPage : ContentPage
{
    private readonly MessagingViewModel _vm;

    public MessagingPage(MessagingViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;

        JwtDelegatingHandler.UnauthorizedAccess += OnUnauthorizedAccess;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadCommand.ExecuteAsync(null);
    }

    private void OnThreadTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is MessageThreadStub thread)
        {
            Shell.Current.GoToAsync(
                $"MessageThreadPage?threadId={Uri.EscapeDataString(thread.ThreadId)}&recipientEmail={Uri.EscapeDataString(thread.OtherPartyEmail)}");
        }
    }

    private async void OnUnauthorizedAccess(object? sender, EventArgs e)
    {
        _vm.ErrorMessage = "Your session expired. Please sign in again.";
        await Shell.Current.GoToAsync("//LoginPage");
    }
}
#endif
