#if MOBILE
using AgendaBuddy.Library.Entities;
using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.ViewModels;

namespace AgendaBuddy.MobileApp.Views;

[QueryProperty(nameof(AppointmentId), "appointmentId")]
[QueryProperty(nameof(CustomerEmail), "customerEmail")]
[QueryProperty(nameof(CustomerName), "customerName")]
[QueryProperty(nameof(CustomerPhone), "customerPhone")]
[QueryProperty(nameof(ProviderName), "providerName")]
[QueryProperty(nameof(DisplayName), "displayName")]
[QueryProperty(nameof(ScheduledAtStr), "scheduledAt")]
[QueryProperty(nameof(StatusStr), "status")]
[QueryProperty(nameof(ServiceName), "serviceName")]
[QueryProperty(nameof(ServiceDurationMinutesStr), "serviceDurationMinutes")]
[QueryProperty(nameof(CustomerNotes), "customerNotes")]
public partial class AppointmentDetailPage : ContentPage
{
    private readonly AppointmentDetailViewModel _viewModel;

    public string AppointmentId { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ScheduledAtStr { get; set; } = string.Empty;
    public string StatusStr { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>Carried as a string because Shell query properties are strings; parsed below.</summary>
    public string ServiceDurationMinutesStr { get; set; } = string.Empty;
    public string CustomerNotes { get; set; } = string.Empty;

    public AppointmentDetailPage(AppointmentDetailViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;

        _viewModel.ActionRequested += OnActionRequested;
        JwtDelegatingHandler.UnauthorizedAccess += OnUnauthorizedAccess;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.AppointmentId = AppointmentId;

        // Only build a fallback when the navigation actually carried appointment data. A notification tap and a
        // push tap arrive with the id ALONE, and a fallback built from empty query properties is not a fallback
        // — it is a fabricated appointment scheduled for "now" and marked Requested, which is what the reader
        // would then be shown for something that had been cancelled. No fallback means a failed fetch surfaces
        // as a failed fetch.
        var fallback = HasNavigationPayload ? BuildFallback() : null;

        _viewModel.LoadWithFallbackCommand.Execute(fallback);
        _viewModel.LoadNotesCommand.Execute(null);
    }

    /// <summary>
    /// Whether the caller supplied enough to render without a fetch. <c>scheduledAt</c> is the tell: every
    /// caller that has the appointment in hand passes it, and no caller that only has an identifier can.
    /// </summary>
    private bool HasNavigationPayload => !string.IsNullOrWhiteSpace(ScheduledAtStr);

    private AppointmentDetail BuildFallback()
    {
        return new AppointmentDetail
        {
            Id = AppointmentId,
            CustomerEmail = CustomerEmail,
            CustomerName = CustomerName,
            CustomerPhone = CustomerPhone,
            ProviderName = ProviderName,
            DisplayName = string.IsNullOrEmpty(DisplayName) ? CustomerName : DisplayName,
            ContactEmail = CustomerEmail,
            ContactPhone = CustomerPhone,
            ScheduledAt = DateTime.TryParse(ScheduledAtStr, out var dt) ? dt : DateTime.Now,
            Status = Enum.TryParse<AppointmentStatus>(StatusStr, out var st) ? st : AppointmentStatus.Requested,
            ServiceName = ServiceName,
            ServiceDurationMinutes = int.TryParse(ServiceDurationMinutesStr, out var minutes) ? minutes : null,
            CustomerNotes = CustomerNotes
        };
    }

    private async void OnActionRequested(object? sender, AppointmentActionEventArgs e)
    {
        switch (e.Action)
        {
            case ActionType.Confirm:
                // The dedicated status route only accepts Booked or Completed as a target
                // (AppointmentEntity.TransitionTo) — Confirmed is not a legal transition through it. "Confirm"
                // maps to the real Requested→Booked transition, which either participant may perform.
                await _viewModel.ExecuteStatusUpdateAsync(AppointmentStatus.Booked);
                break;

            case ActionType.Cancel:
                var cancelChoice = await DisplayActionSheetAsync(
                    "Cancel this appointment?",
                    "Keep it",
                    null,
                    "Cancel appointment");
                if (cancelChoice == "Cancel appointment")
                {
                    var cancelled = await _viewModel.ExecuteCancelAsync();
                    if (cancelled)
                        await Shell.Current.GoToAsync("..");
                }
                break;

            case ActionType.Complete:
                var completeChoice = await DisplayActionSheetAsync(
                    "Mark this appointment as complete?",
                    "Go back",
                    null,
                    "Mark complete");
                if (completeChoice == "Mark complete")
                    await _viewModel.ExecuteStatusUpdateAsync(AppointmentStatus.Completed);
                break;
        }
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnViewPaymentClicked(object? sender, EventArgs e)
    {
        var nav = new Dictionary<string, object> { ["appointmentId"] = _viewModel.AppointmentId };
        await Shell.Current.GoToAsync("payment", nav);
    }

    private void OnUnauthorizedAccess(object? sender, EventArgs e)
    {
        _viewModel.ErrorMessage = "Your session expired. Any unsaved changes were not saved.";
    }
}
#endif
