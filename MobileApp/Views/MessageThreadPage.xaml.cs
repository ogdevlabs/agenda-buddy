#if MOBILE
using MobileApp.Infrastructure;
using MobileApp.ViewModels;

namespace MobileApp.Views;

[QueryProperty(nameof(ThreadId), "threadId")]
[QueryProperty(nameof(RecipientEmail), "recipientEmail")]
public partial class MessageThreadPage : ContentPage
{
    private readonly MessageThreadViewModel _vm;

    public string ThreadId
    {
        set
        {
            _vm.ThreadId = value;
        }
    }

    public string RecipientEmail
    {
        set
        {
            _vm.RecipientEmail = value;
        }
    }

    public MessageThreadPage(MessageThreadViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;

        // UX F-001: handle 401 while composing a message
        JwtDelegatingHandler.UnauthorizedAccess += OnUnauthorizedAccess;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadThreadCommand.ExecuteAsync(null);
    }

    private async void OnUnauthorizedAccess(object? sender, EventArgs e)
    {
        _vm.ErrorMessage = "Your session expired. Your in-progress message was not sent — please sign in again.";
        await Shell.Current.GoToAsync("//LoginPage");
    }
}
#endif
