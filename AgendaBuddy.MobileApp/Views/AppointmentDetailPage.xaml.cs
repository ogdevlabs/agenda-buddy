#if MOBILE
using AgendaBuddy.Library.Entities;
using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Resources.Strings;
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
[QueryProperty(nameof(ContactEmailStr), "contactEmail")]
[QueryProperty(nameof(ContactAvatarId), "contactAvatarId")]
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

    /// <summary>
    /// The counterpart from the CALLER's side, which is not always the customer.
    /// </summary>
    /// <remarks>
    /// The older query properties are all customer-shaped because the page was built for a provider looking at
    /// their customer. A customer looking at their provider has the opposite counterpart, so the avatar and the
    /// contact row need the resolved one rather than <c>customerEmail</c>.
    /// </remarks>
    public string ContactEmailStr { get; set; } = string.Empty;

    /// <summary>The assigned mark the calling list already resolved, so both surfaces show the same one.</summary>
    public string ContactAvatarId { get; set; } = string.Empty;
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
            // The caller's resolved counterpart when it travelled; the customer address is the pre-existing
            // fallback, which is right for a provider and wrong for a customer -- hence preferring the former.
            ContactEmail = string.IsNullOrWhiteSpace(ContactEmailStr) ? CustomerEmail : ContactEmailStr,
            ContactAvatarId = ContactAvatarId,
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
                // The confirmation NAMES the session rather than asking a bare "are you sure?". Cancelled is
                // terminal (ADR-037), so this is the last chance to notice it is the wrong appointment.
                var cancelChoice = await DisplayActionSheetAsync(
                    AppResources.Format("Appointment_CancelPrompt", SessionDescription()),
                    AppResources.GetString("Appointment_KeepAction"),
                    null,
                    AppResources.GetString("Appointment_CancelAction"));
                if (cancelChoice == AppResources.GetString("Appointment_CancelAction"))
                {
                    var cancelled = await _viewModel.ExecuteCancelAsync();
                    if (cancelled)
                        await Shell.Current.GoToAsync("..");
                }
                break;


            // Both open the SAME page: the interaction is identical, and RescheduleViewModel words itself from
            // the session role. Everything it needs travels with the navigation, because this page already holds
            // the appointment -- passing the id alone would make it re-fetch what was just read, and the service
            // name is what sizes the slots to the length that was agreed.
            case ActionType.Reschedule:
            case ActionType.RequestNewTime:
                await OpenReschedulePageAsync();
                break;

            case ActionType.ApproveReschedule:
                var approveChoice = await DisplayActionSheetAsync(
                    $"Move this session to {_viewModel.ProposedTimeLabel}?",
                    "Not now",
                    null,
                    "Approve");
                if (approveChoice == "Approve")
                    await _viewModel.ExecuteRescheduleAnswerAsync(approve: true);
                break;

            case ActionType.DeclineReschedule:
                // Declining is destructive to the other party's request, so it is confirmed too -- and the
                // confirmation states what declining LEAVES, which is the thing a reader wants to be sure of.
                var declineChoice = await DisplayActionSheetAsync(
                    "Decline the new time? The session stays where it is.",
                    "Not now",
                    null,
                    "Decline");
                if (declineChoice == "Decline")
                    await _viewModel.ExecuteRescheduleAnswerAsync(approve: false);
                break;
        }
    }

    /// <summary>
    /// Opens the slot picker for this appointment, on the PROVIDER'S calendar whichever side is asking.
    /// </summary>
    private async Task OpenReschedulePageAsync()
    {
        var appointment = _viewModel.Appointment;
        if (appointment is null) return;

        var nav = new Dictionary<string, object>
        {
            ["appointmentId"] = _viewModel.AppointmentId,
            ["providerEmail"] = appointment.ProviderEmail,
            ["serviceName"] = appointment.ServiceName ?? string.Empty,

            // Round-trip format: a Shell query property arrives as text, and a culture-formatted date would be
            // reinterpreted by whatever the device's culture happens to be on the way back in.
            ["currentStart"] = appointment.ScheduledAt.ToString("o")
        };

        await Shell.Current.GoToAsync("reschedule", nav);
    }

    /// <summary>
    /// The session in words, for a confirmation that has to be unambiguous about WHICH appointment it means.
    /// </summary>
    private string SessionDescription()
    {
        var appointment = _viewModel.Appointment;
        if (appointment is null) return "this appointment";

        var service = string.IsNullOrWhiteSpace(appointment.ServiceName)
            ? "this session"
            : appointment.ServiceName;

        return $"{service} on {appointment.ScheduledAt:ddd d MMM 'at' h:mm tt}";
    }


    // OnViewPaymentClicked removed with the Payment tab's button: payments are being handled outside this
    // screen. PaymentPage and its Shell route are left in place — nothing navigates to them from here now, and
    // deleting a working screen for a redesign that has not landed yet would be throwing it away early.

    private void OnUnauthorizedAccess(object? sender, EventArgs e)
    {
        _viewModel.ErrorMessage = AppResources.GetString("Session_ExpiredUnsaved");
    }
}
#endif
