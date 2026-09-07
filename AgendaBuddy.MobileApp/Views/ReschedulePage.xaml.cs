#if MOBILE
using AgendaBuddy.MobileApp.ViewModels;

namespace AgendaBuddy.MobileApp.Views;

/// <summary>
/// Picks a new time for an existing session — the provider's move, or the customer's request.
/// </summary>
/// <remarks>
/// Everything it needs travels as a Shell query property, because the caller (AppointmentDetailPage) already has
/// the appointment in hand: passing the identifier alone would make this page re-fetch what its opener just read,
/// and the service name and current start are what size the slots and label the change.
/// </remarks>
[QueryProperty(nameof(AppointmentId), "appointmentId")]
[QueryProperty(nameof(ProviderEmail), "providerEmail")]
[QueryProperty(nameof(ServiceName), "serviceName")]
[QueryProperty(nameof(CurrentStart), "currentStart")]
public partial class ReschedulePage : ContentPage
{
    private readonly RescheduleViewModel _viewModel;

    public ReschedulePage(RescheduleViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;

        _viewModel.Completed += OnCompleted;
    }

    public string AppointmentId { get; set; } = string.Empty;
    public string ProviderEmail { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// The session's current start, as a round-trippable string.
    /// </summary>
    /// <remarks>
    /// A string rather than a <c>DateTime</c> because a Shell query property arrives as text; parsed round-trip
    /// ("o") so the value cannot be reinterpreted by the device's culture on the way through.
    /// </remarks>
    public string CurrentStart { get; set; } = string.Empty;

    protected override void OnAppearing()
    {
        base.OnAppearing();

        _viewModel.AppointmentId = Uri.UnescapeDataString(AppointmentId);
        _viewModel.ProviderEmail = Uri.UnescapeDataString(ProviderEmail);

        var service = Uri.UnescapeDataString(ServiceName);
        _viewModel.ServiceName = string.IsNullOrWhiteSpace(service) ? null : service;

        if (DateTime.TryParse(
                Uri.UnescapeDataString(CurrentStart),
                null,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out var currentStart))
        {
            _viewModel.CurrentStart = currentStart;
        }

        _viewModel.LoadCommand.Execute(null);
    }

    /// <summary>
    /// Returns to the appointment, whose own OnAppearing re-reads it — so the moved time or the new pending
    /// proposal is what shows, without this page reaching into that one.
    /// </summary>
    private async void OnCompleted(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");

    private async void OnBackClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");
}
#endif
