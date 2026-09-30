#if MOBILE
using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Services;
using AgendaBuddy.MobileApp.ViewModels;

namespace AgendaBuddy.MobileApp.Views;

[QueryProperty(nameof(ThreadId), "threadId")]
[QueryProperty(nameof(RecipientEmail), "recipientEmail")]
[QueryProperty(nameof(CounterpartName), "counterpartName")]
public partial class MessageThreadPage : ContentPage
{
    private readonly MessageThreadViewModel _vm;
    private readonly ShowcaseLinkViewModel _showcaseLink;
    private readonly IUserSessionService _session;

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

    /// <summary>Optional: the counterpart's real name, so the header is not a raw email address.</summary>
    public string CounterpartName
    {
        set
        {
            _vm.CounterpartName = value;
        }
    }

    public MessageThreadPage(MessageThreadViewModel vm, ShowcaseLinkViewModel showcaseLink, IUserSessionService session)
    {
        InitializeComponent();
        _vm = vm;
        _showcaseLink = showcaseLink;
        _session = session;
        BindingContext = vm;

        _showcaseLink.Source = Routing.ShowcaseSource.Message;
        WorkStrip.BindingContext = _showcaseLink;
        _showcaseLink.ShowcaseRequested += OnShowcaseRequested;

        // Handle 401 while composing a message
        JwtDelegatingHandler.UnauthorizedAccess += OnUnauthorizedAccess;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_session.IsCustomer)
            _ = _showcaseLink.LoadAsync(_vm.RecipientEmail, _vm.CounterpartName);
        await _vm.LoadThreadCommand.ExecuteAsync(null);
    }

    private async void OnShowcaseRequested(object? sender, ShowcaseRequestedEventArgs e) =>
        await Shell.Current.GoToAsync(ShowcaseNavigation.Route, e.Parameters);

    private async void OnUnauthorizedAccess(object? sender, EventArgs e)
    {
        _vm.ErrorMessage = AppResources.GetString("Session_ExpiredMessage");
        await Shell.Current.GoToAsync("//login");
    }
}
#endif
