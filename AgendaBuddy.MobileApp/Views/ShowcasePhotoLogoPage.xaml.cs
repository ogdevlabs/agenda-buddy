#if MOBILE
using AgendaBuddy.MobileApp.ViewModels;

namespace AgendaBuddy.MobileApp.Views;

public partial class ShowcasePhotoLogoPage : ContentPage
{
    private readonly ShowcasePhotoLogoViewModel _viewModel;

    public ShowcasePhotoLogoPage(ShowcasePhotoLogoViewModel viewModel)
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
}
#endif
