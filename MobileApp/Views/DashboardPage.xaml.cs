#if MOBILE
using MobileApp.ViewModels;

namespace MobileApp.Views;

public partial class DashboardPage : ContentPage
{
    private readonly DashboardViewModel _viewModel;

    public DashboardPage(DashboardViewModel viewModel)
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

    private async void OnAppointmentSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not MobileApp.Models.AppointmentSummary selected)
            return;

        // Clear selection so the user can tap the same item again
        if (sender is CollectionView cv)
            cv.SelectedItem = null;

        await Shell.Current.GoToAsync($"AppointmentDetailPage?id={selected.Id}");
    }
}
#endif
