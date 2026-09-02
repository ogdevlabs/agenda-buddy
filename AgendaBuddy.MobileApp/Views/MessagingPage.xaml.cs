#if MOBILE
using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.ViewModels;

namespace AgendaBuddy.MobileApp.Views;

public partial class MessagingPage : ContentPage
{
    private readonly MessagingViewModel _vm;

    public MessagingPage(MessagingViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;

        JwtDelegatingHandler.UnauthorizedAccess += OnUnauthorizedAccess;
        _vm.ThreadOpenRequested += OnThreadOpenRequested;
    }

    private async void OnThreadOpenRequested(object? sender, MessageThreadStub thread)
    {
        var nav = new Dictionary<string, object>
        {
            ["threadId"] = thread.ThreadId,
            ["recipientEmail"] = thread.OtherPartyEmail
        };
        await Shell.Current.GoToAsync("messageThread", nav);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadCommand.ExecuteAsync(null);
    }

    private async void OnUnauthorizedAccess(object? sender, EventArgs e)
    {
        _vm.ErrorMessage = "Your session expired. Please sign in again.";
        await Shell.Current.GoToAsync("//login");
    }
}
#endif
