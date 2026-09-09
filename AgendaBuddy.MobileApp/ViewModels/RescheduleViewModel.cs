using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Resources.Strings;
using AgendaBuddy.MobileApp.Services;

namespace AgendaBuddy.MobileApp.ViewModels;

/// <summary>
/// "Move this session" for a provider, "ask for a new time" for a customer. One screen, two verbs.
/// </summary>
/// <remarks>
/// <para>
/// One page rather than two, because the interaction is identical — see the provider's real free slots, pick one,
/// confirm — and only the wording and the route differ. Two pages would be two copies of the slot picking, the
/// timezone handling and the confirmation, free to drift apart.
/// </para>
/// <para>
/// The picking itself is <see cref="SlotPickerViewModel"/>, shared with booking, so the rules that are easy to get
/// quietly wrong live in one place: slots grouped by LOCAL date while holding UTC instants, landing on the soonest
/// date with room, dropping the chosen slot when the date changes.
/// </para>
/// </remarks>
public partial class RescheduleViewModel : ObservableObject
{
    private readonly IBookingApiService _bookingApiService;
    private readonly IUserSessionService _session;

    public RescheduleViewModel(
        IBookingApiService bookingApiService,
        ICalendarApiService calendarApiService,
        IUserSessionService session)
    {
        _bookingApiService = bookingApiService;
        _session = session;

        Picker = new SlotPickerViewModel(calendarApiService);
        Picker.SelectionChanged += (_, _) => NotifyDerived();
    }

    /// <summary>The date strip, time chips and selection. Shared with the booking flow.</summary>
    public SlotPickerViewModel Picker { get; }

    /// <summary>Which appointment is being moved. Set from a Shell query property before loading.</summary>
    public string AppointmentId { get; set; } = string.Empty;

    /// <summary>Whose calendar to offer — always the PROVIDER'S, whichever side is asking.</summary>
    public string ProviderEmail { get; set; } = string.Empty;

    /// <summary>The service, so slots are sized to the length that was actually agreed.</summary>
    public string? ServiceName { get; set; }

    /// <summary>Where the session is now, on this device's clock — shown so the change is comparable.</summary>
    public DateTime CurrentStart { get; set; }

    [ObservableProperty]
    private bool _isSubmitting;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    /// <summary>
    /// A provider moves the session; a customer asks. The distinction is the product decision — the calendar is
    /// the provider's — and it drives every string on this page as well as which route is called.
    /// </summary>
    public bool IsProviderMovingIt => _session.IsProvider;

    public string Title => AppResources.GetString(
        IsProviderMovingIt ? "Reschedule_RescheduleTitle" : "Reschedule_RequestTitle");

    public string Explanation => IsProviderMovingIt
                ? AppResources.GetString("Reschedule_ProviderExplanation")
                : AppResources.GetString("Reschedule_CustomerExplanation");

    public string CurrentTimeLabel => AppResources.Format("Reschedule_CurrentTime", CurrentStart);

    public string SubmitLabel => AppResources.GetString(
        IsProviderMovingIt ? "Reschedule_MoveSession" : "Reschedule_SendRequest");

    public bool CanSubmit => Picker.SelectedStartUtc is not null && !IsSubmitting;

    /// <summary>Raised on success, so the page can leave and the detail view can re-read the appointment.</summary>
    public event EventHandler? Completed;

    [RelayCommand]
    private async Task LoadAsync()
    {
        ErrorMessage = string.Empty;

        Picker.ProviderEmail = ProviderEmail;
        Picker.ServiceName = ServiceName;

        await Picker.LoadAsync();

        // The picker words its own failure; this surfaces it on the page's one banner, so there is a single place
        // the reader looks for what went wrong.
        if (Picker.HasError) ErrorMessage = Picker.ErrorMessage;

        NotifyDerived();
    }

    [RelayCommand(CanExecute = nameof(CanSubmit))]
    private async Task SubmitAsync()
    {
        if (Picker.SelectedStartUtc is not { } startUtc) return;

        IsSubmitting = true;
        ErrorMessage = string.Empty;
        NotifyDerived();

        try
        {
            // The server's own instant, sent back unchanged. Sending the local rendering would book a different
            // time than the one on screen.
            var result = IsProviderMovingIt
                ? await _bookingApiService.RescheduleAsync(AppointmentId, startUtc)
                : await _bookingApiService.RequestRescheduleAsync(AppointmentId, startUtc);

            if (!result.Succeeded)
            {
                ErrorMessage = result.ErrorMessage ?? AppResources.Error_ActionRejected;
                await ToastNotifier.ShowAsync(ErrorMessage);

                // Most likely somebody took the slot between the fetch and the tap. Re-read so the stale slot
                // disappears rather than being offered again — the same recovery the booking flow makes.
                await Picker.LoadAsync();
                return;
            }

            await ToastNotifier.ShowAsync(AppResources.GetString(IsProviderMovingIt
                ? "Reschedule_SessionMoved"
                : "Reschedule_RequestSent"));

            Completed?.Invoke(this, EventArgs.Empty);
        }
        catch (GatewayServiceUnavailableException exception)
        {
            ErrorMessage = GatewayErrorMapper.Describe(exception.FailedService);
            await ToastNotifier.ShowAsync(ErrorMessage);
        }
        finally
        {
            IsSubmitting = false;
            NotifyDerived();
        }
    }

    private void NotifyDerived()
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(CanSubmit));
        OnPropertyChanged(nameof(CurrentTimeLabel));
        SubmitCommand.NotifyCanExecuteChanged();
    }

    partial void OnErrorMessageChanged(string value) => NotifyDerived();
    partial void OnIsSubmittingChanged(bool value) => NotifyDerived();
}
