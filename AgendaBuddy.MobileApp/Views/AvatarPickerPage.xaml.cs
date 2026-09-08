#if MOBILE
using AgendaBuddy.MobileApp.ViewModels;

namespace AgendaBuddy.MobileApp.Views;

public partial class AvatarPickerPage : ContentPage
{
    private readonly AvatarPickerViewModel _viewModel;

    public AvatarPickerPage(AvatarPickerViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;

        _viewModel.Saved += OnSaved;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadCommand.Execute(null);
    }

    /// <summary>
    /// Returns to Profile, whose <c>OnAppearing</c> re-reads the profile — so the hero shows the new mark without
    /// this page reaching into it.
    /// </summary>
    private async void OnSaved(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");
}
#endif
