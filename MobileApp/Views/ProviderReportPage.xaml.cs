#if MOBILE
using MobileApp.ViewModels;

namespace MobileApp.Views;

public partial class ProviderReportPage : ContentPage
{
    private readonly ProviderReportViewModel _viewModel;

    public ProviderReportPage(ProviderReportViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadCommand.Execute(null);
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
#endif
