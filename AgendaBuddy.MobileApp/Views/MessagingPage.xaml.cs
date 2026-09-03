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

    /// <summary>
    /// Sends the user to their contact list, which is where a conversation can actually be started: it is
    /// the only place that knows who they are allowed to message. <c>//customers</c> is the shared route —
    /// that tab is titled Providers for a Customer and Customers for a Provider.
    /// </summary>
    private async void OnNewMessageClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("//customers");

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
