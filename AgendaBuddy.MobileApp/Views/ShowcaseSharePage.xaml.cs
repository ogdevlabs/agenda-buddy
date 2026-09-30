#if MOBILE
using System.ComponentModel;
using AgendaBuddy.MobileApp.ViewModels;

namespace AgendaBuddy.MobileApp.Views;

public partial class ShowcaseSharePage : ContentPage
{
    private readonly ShowcaseShareViewModel _viewModel;
    private bool _loaded;

    public ShowcaseSharePage(ShowcaseShareViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
        _viewModel.PropertyChanged += OnViewModelChanged;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_loaded && _viewModel.HasCode)
            return;
        _loaded = true;
        _viewModel.LoadCommand.Execute(null);
    }

    private async void OnShareLinkClicked(object? sender, EventArgs e)
    {
        if (!_viewModel.HasCode)
            return;

        try
        {
            await Share.Default.RequestAsync(new ShareTextRequest
            {
                Text = _viewModel.LinkShareText,
                Uri = _viewModel.Url
            });
        }
        catch (Exception)
        {
            // The three image formats below remain; a failed text share has nothing further to offer.
        }
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ShowcaseShareViewModel.QrPng))
            return;

        var png = _viewModel.QrPng;
        QrImage.Source = png is null ? null : ImageSource.FromStream(() => new MemoryStream(png));
    }
}
#endif
