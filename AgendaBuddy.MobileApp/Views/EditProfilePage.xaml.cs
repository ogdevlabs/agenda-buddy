#if MOBILE
using AgendaBuddy.MobileApp.ViewModels;

namespace AgendaBuddy.MobileApp.Views;

public partial class EditProfilePage : ContentPage
{
    private readonly EditProfileViewModel _viewModel;

    public EditProfilePage(EditProfileViewModel viewModel)
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

    private async void OnTermsTapped(object? sender, EventArgs e) => await Shell.Current.GoToAsync("terms");

    private async void OnPrivacyTapped(object? sender, EventArgs e) => await Shell.Current.GoToAsync("privacy");

    /// <summary>
    /// Returns to Profile, whose <c>OnAppearing</c> re-reads the profile — so the hero shows the new name without
    /// this page reaching into it.
    /// </summary>
    private async void OnSaved(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");
}
#endif
