#if MOBILE
using AgendaBuddy.MobileApp.ViewModels;

namespace AgendaBuddy.MobileApp.Views;

public partial class CustomersPage : ContentPage
{
    private readonly CustomersViewModel _viewModel;

    public CustomersPage(CustomersViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;

        _viewModel.BookRequested += OnBookRequested;
        _viewModel.MessageRequested += OnMessageRequested;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadCommand.Execute(null);
    }

    /// <summary>
    /// Opens the message thread with a contact. This is the entry point a new conversation needs: the
    /// Messages tab can only list threads that already exist, so without it neither side could ever say
    /// anything first.
    /// </summary>
    private async void OnMessageRequested(object? sender, Models.CustomerSummary contact)
    {
        // "recipientEmail" is the key MessageThreadPage declares; MessageService derives the thread id
        // from both addresses, so a thread with nothing in it yet needs no id — the counterpart is enough.
        var nav = new Dictionary<string, object>
        {
            ["recipientEmail"] = contact.Email,
            ["counterpartName"] = contact.FullName
        };
        await Shell.Current.GoToAsync("messageThread", nav);
    }

    private async void OnBookRequested(object? sender, BookRequestedEventArgs e)
    {
        var nav = new Dictionary<string, object>
        {
            ["counterpartEmail"] = e.CounterpartEmail,
            ["counterpartName"] = e.CounterpartName,
            ["profession"] = e.Profession ?? string.Empty
        };
        await Shell.Current.GoToAsync("book", nav);
    }
}
#endif
