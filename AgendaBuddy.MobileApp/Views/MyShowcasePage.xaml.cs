#if MOBILE
using AgendaBuddy.MobileApp.ViewModels;

namespace AgendaBuddy.MobileApp.Views;

public partial class MyShowcasePage : ContentPage
{
    private readonly MyShowcaseViewModel _viewModel;

    public MyShowcasePage(MyShowcaseViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    /// <summary>Re-read on every appearance: each card's editor is a pushed page, and returning is the refresh.</summary>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadCommand.Execute(null);
    }

    private async void OnPhotoLogoClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("showcasePhotoLogo");

    private async void OnTextClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("showcaseText");

    private async void OnPortfolioClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("portfolioEditor");

    private async void OnShareClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("showcaseShare");
}
#endif
