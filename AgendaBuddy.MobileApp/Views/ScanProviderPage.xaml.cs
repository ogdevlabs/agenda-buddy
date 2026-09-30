#if MOBILE
using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Routing;
using AgendaBuddy.MobileApp.Services;
using AgendaBuddy.MobileApp.ViewModels;
using ZXing.Net.Maui;

namespace AgendaBuddy.MobileApp.Views;

public partial class ScanProviderPage : ContentPage
{
    private readonly ScanProviderViewModel _viewModel;
    private readonly IInAppAlertService _alerts;

    public ScanProviderPage(ScanProviderViewModel viewModel, IInAppAlertService alerts)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _alerts = alerts;
        BindingContext = _viewModel;
        Reader.Options = new BarcodeReaderOptions
        {
            Formats = BarcodeFormat.QrCode,
            AutoRotate = true,
            Multiple = false
        };
        _viewModel.ShowcaseResolved += OnShowcaseResolved;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.StartCommand.Execute(null);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.StopCommand.Execute(null);
    }

    private void OnBarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        var payload = e.Results?.FirstOrDefault()?.Value;
        MainThread.BeginInvokeOnMainThread(() => _ = _viewModel.HandleScannedAsync(payload));
    }

    private async void OnShowcaseResolved(object? sender, ShowcaseResolvedEventArgs e)
    {
        var nav = new Dictionary<string, object>
        {
            ["showcase"] = e.View,
            ["source"] = e.Source
        };
        await Shell.Current.GoToAsync("providerShowcase", nav);
        _ = _alerts.ShowAsync(ShowcaseText.OpenedFromCode(e.View.FirstName));
    }
}
#endif
