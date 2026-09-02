#if MOBILE
using AgendaBuddy.MobileApp.ViewModels;

namespace AgendaBuddy.MobileApp.Views;

public partial class ProfessionsPage : ContentPage
{
    private readonly ProfessionsViewModel _viewModel;

    public ProfessionsPage(ProfessionsViewModel viewModel)
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

    private async void OnContinueToServicesClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("services");
    }
}
#endif
