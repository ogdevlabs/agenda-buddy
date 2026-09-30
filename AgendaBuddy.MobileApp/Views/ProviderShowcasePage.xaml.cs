#if MOBILE
using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Resources.Strings;
using AgendaBuddy.MobileApp.Routing;
using AgendaBuddy.MobileApp.Services;
using AgendaBuddy.MobileApp.ViewModels;

namespace AgendaBuddy.MobileApp.Views;

/// <summary>
/// Query keys: <c>providerRef</c> (required unless <c>showcase</c> is passed), <c>source</c>
/// (a <see cref="ShowcaseSource"/> or its name), <c>email</c> (when the caller already knows it), and
/// <c>showcase</c> (a <see cref="ShowcaseView"/> already fetched by a scan, so the visit is not recorded twice).
/// </summary>
public partial class ProviderShowcasePage : ContentPage, IQueryAttributable
{
    private static readonly ShowcaseReportReason[] ReportReasons =
    [
        ShowcaseReportReason.Inappropriate,
        ShowcaseReportReason.NotTheirWork,
        ShowcaseReportReason.Spam,
        ShowcaseReportReason.Other
    ];

    private readonly ProviderShowcaseViewModel _viewModel;
    private readonly IInAppAlertService _alerts;

    public ProviderShowcasePage(ProviderShowcaseViewModel viewModel, IInAppAlertService alerts)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _alerts = alerts;
        BindingContext = _viewModel;
        _viewModel.BookRequested += OnBookRequested;
        _viewModel.MessageRequested += OnMessageRequested;
        _viewModel.PhotoRequested += OnPhotoRequested;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        var source = ShowcaseSource.Directory;
        if (query.TryGetValue("source", out var rawSource))
        {
            source = rawSource switch
            {
                ShowcaseSource typed => typed,
                string name when Enum.TryParse<ShowcaseSource>(Uri.UnescapeDataString(name), true, out var parsed) => parsed,
                _ => ShowcaseSource.Directory
            };
        }

        if (query.TryGetValue("email", out var email) && email is string address)
            _viewModel.ProviderEmail = Uri.UnescapeDataString(address);

        if (query.TryGetValue("showcase", out var preloaded) && preloaded is ShowcaseView view)
        {
            _viewModel.Preload(view, source);
            return;
        }

        _viewModel.Source = source;
        if (query.TryGetValue("providerRef", out var providerRef) && providerRef is string reference)
            _viewModel.ProviderRef = Uri.UnescapeDataString(reference);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadCommand.Execute(null);
    }

    private async void OnMoreClicked(object? sender, EventArgs e)
    {
        var report = AppResources.GetString("ProviderShowcase_Report");
        var hide = AppResources.GetString("ProviderShowcase_Hide");
        var options = _viewModel.CanHide ? new[] { report, hide } : new[] { report };
        var chosen = await DisplayActionSheet(
            AppResources.GetString("ProviderShowcase_MoreOptions"),
            AppResources.GetString("General_Cancel"),
            null,
            options);

        if (chosen == report)
            await ReportAsync();
        else if (chosen == hide)
            await HideAsync();
    }

    private async Task ReportAsync()
    {
        var cancel = AppResources.GetString("General_Cancel");
        var labels = ReportReasons.Select(ProviderShowcaseViewModel.ReasonLabel).ToArray();
        var chosen = await DisplayActionSheet(AppResources.GetString("ShowcaseReport_Title"), cancel, null, labels);
        var index = Array.IndexOf(labels, chosen);
        if (index < 0)
            return;

        var reason = ReportReasons[index];
        var required = reason == ShowcaseReportReason.Other;
        var detail = await DisplayPromptAsync(
            labels[index],
            AppResources.GetString(required ? "ShowcaseReport_DetailPrompt" : "ShowcaseReport_DetailOptional"),
            AppResources.GetString("ShowcaseReport_Send"),
            cancel,
            null,
            ShowcaseReportReasons.DetailMaxLength,
            Keyboard.Text);
        if (detail is null)
            return;

        var outcome = await _viewModel.ReportAsync(reason, detail);
        await _alerts.ShowAsync(outcome ?? _viewModel.ErrorMessage);
    }

    private async Task HideAsync()
    {
        var confirmed = await DisplayAlert(
            AppResources.GetString("ProviderShowcase_Hide"),
            _viewModel.HidePrompt,
            AppResources.GetString("ProviderShowcase_Hide"),
            AppResources.GetString("General_Keep"));
        if (!confirmed || !await _viewModel.HideAsync())
            return;

        var firstName = _viewModel.FirstName;
        await Shell.Current.GoToAsync("..");
        _ = _alerts.ShowAsync(
            ShowcaseText.Hidden(firstName),
            AppResources.GetString("General_Undo"),
            async () => await _viewModel.UnhideAsync());
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

    private async void OnMessageRequested(object? sender, CustomerSummary contact)
    {
        var nav = new Dictionary<string, object>
        {
            ["recipientEmail"] = contact.Email,
            ["counterpartName"] = contact.FullName
        };
        await Shell.Current.GoToAsync("messageThread", nav);
    }

    private async void OnPhotoRequested(object? sender, ShowcasePhotoRequestedEventArgs e)
    {
        var nav = new Dictionary<string, object>
        {
            ["tiles"] = e.Tiles,
            ["index"] = e.Index
        };
        await Shell.Current.GoToAsync("portfolioViewer", nav);
    }
}
#endif
