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
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadCommand.Execute(null);
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
