using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Resources.Strings;
using AgendaBuddy.MobileApp.Services;

namespace AgendaBuddy.MobileApp.ViewModels;

/// <summary>
/// Books an appointment by picking a service, then a date, then one of the provider's actually-free times.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing is typed.</b> This used to offer a bare <c>DatePicker</c>/<c>TimePicker</c> and let either
/// party propose any instant, because <c>GET /api/v1/calendar/availability/{email}</c> was
/// ownership-guarded and a customer could not read a provider's free/busy at all. That guard is gone, so
/// the flow is now: choose a service → fetch that provider's availability sized to the service's duration →
/// choose from the dates that have room → choose from that date's free times. A slot the provider does not
/// have is no longer expressible in the UI.
/// </para>
/// <para>
/// The 90-day window is fetched <b>once per service</b> and grouped by date, so moving between dates costs
/// nothing. It is re-fetched when the service changes, because slot boundaries depend on that service's
/// duration — a 90-minute service has strictly fewer valid starts than a 30-minute one.
/// </para>
/// <para>
/// Services always belong to the PROVIDER, whichever side is booking: a provider booking a customer offers
/// their own catalogue, a customer booking a provider offers that provider's. Only the bookable ones are
/// shown — active, and classified under a profession — matching what the server will accept.
/// </para>
/// </remarks>
public partial class BookAppointmentViewModel : ObservableObject
{
    private readonly IBookingApiService _bookingApiService;
    private readonly IServicesApiService _servicesApiService;
    private readonly ICalendarApiService _calendarApiService;
    private readonly IUserSessionService _session;

    /// <summary>How far ahead to offer. The server clamps to the same ceiling.</summary>
    public const int WindowDays = 90;

    /// <summary>Fallback session length for a service saved without one.</summary>
    public const int DefaultDurationMinutes = 60;

    [ObservableProperty]
    private List<ServiceItem> _services = new();

    [ObservableProperty]
    private ServiceItem? _selectedService;

    /// <summary>
    /// The date strip, the time chips and the chosen slot. Shared with the reschedule flows rather than
    /// reimplemented here — the timezone-sensitive parts of picking a slot are the same wherever it is done, and
    /// three copies is three places for the same off-by-one-day bug.
    /// </summary>
    public SlotPickerViewModel Picker { get; }

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isBooking;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    /// <summary>The provider being booked when a customer books; the customer when a provider books.</summary>
    public string CounterpartEmail { get; set; } = string.Empty;

    /// <summary>
    /// Observable, unlike <see cref="CounterpartEmail"/>: the page assigns it from a Shell query property
    /// in OnAppearing, which runs AFTER the header binding has already been evaluated. As a plain property
    /// it left the title blank.
    /// </summary>
    [ObservableProperty]
    private string _counterpartName = string.Empty;

    /// <summary>
    /// Optional scope carried over from the directory's profession filter, so a customer who filtered to
    /// "Fitness" is not then offered that provider's unrelated services.
    /// </summary>
    public string? ProfessionScope { get; set; }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool HasServices => Services.Count > 0;
    public bool HasNoServices => !IsLoading && Services.Count == 0;
    public bool HasSelectedService => SelectedService is not null;
    public bool CanBook => SelectedService is not null && Picker.SelectedSlot is not null && !IsBooking;

    /// <summary>True once a service is chosen but the provider has no room at all in the window.</summary>
    public bool IsFullyBooked => HasSelectedService && Picker.IsFullyBooked;

    public string SelectedServiceLabel => SelectedService is null
        ? AppResources.GetString("Book_ChooseService")
        : $"{SelectedService.Name} · {SelectedService.DurationLabel}";

    /// <summary>The chosen slot on this device's clock — never the raw UTC value.</summary>
    public string SelectedSlotLabel => Picker.SelectedSlot is null
        ? string.Empty
        : AppResources.Format("Book_SelectedSlot", Picker.SelectedSlotLabel);

    /// <summary>Prompt shown on the confirm bar before a slot is chosen, so the bar is never a bare button.</summary>
    public string ConfirmPrompt => SelectedService is null
        ? AppResources.GetString("Book_ChooseServiceForTimes")
        : Picker.SelectedSlot is null ? AppResources.GetString("Book_ChooseDateAndTime") : string.Empty;

    public bool ShowConfirmPrompt => Picker.SelectedSlot is null;

    // ── Booking summary ───────────────────────────────────────────────────────────────────────────
    // Everything the customer is committing to, restated at the point of commitment. Each piece was
    // already on screen one step earlier; not repeating it here meant confirming a paid appointment on
    // the strength of a date and a time alone.

    public string SummaryWith => string.IsNullOrWhiteSpace(CounterpartName) ? CounterpartEmail : CounterpartName;

    public string SummaryService => SelectedService?.Name ?? string.Empty;

    public string SummaryPrice => SelectedService?.FeeLabel ?? string.Empty;

    /// <summary>Long-form date, e.g. "Saturday 5 September".</summary>
    public string SummaryDate => Picker.SelectedSlot is null
        ? string.Empty
        : Picker.SelectedSlot.LocalStart.ToString("dddd d MMMM", AppResources.CurrentCulture);

    /// <summary>
    /// Start and end on this device's clock. The end is derived from the service's own duration — the same
    /// arithmetic the booking POST uses — so the window shown is the window booked.
    /// </summary>
    public string SummaryTimeRange
    {
        get
        {
            if (Picker.SelectedSlot is null || SelectedService is null) return string.Empty;
            var start = Picker.SelectedSlot.LocalStart;
            var end = start.AddMinutes(SelectedService.DurationMinutes ?? DefaultDurationMinutes);
            return $"{start.ToString("t", AppResources.CurrentCulture)} – {end.ToString("t", AppResources.CurrentCulture)}";
        }
    }

    public string SummaryDuration => SelectedService is null
        ? string.Empty
        : RuntimeText.Duration(SelectedService.DurationMinutes ?? DefaultDurationMinutes);

    /// <summary>
    /// Names the zone the times above are expressed in. A time with no zone is ambiguous the moment the
    /// customer and provider are not in the same one.
    /// </summary>
    public string SummaryTimeZone
    {
        get
        {
            if (Picker.SelectedSlot is null) return string.Empty;
            return Picker.TimeZoneLabel;
        }
    }

    public event EventHandler<string>? BookingSucceeded;

    public BookAppointmentViewModel(
        IBookingApiService bookingApiService,
        IServicesApiService servicesApiService,
        ICalendarApiService calendarApiService,
        IUserSessionService session)
    {
        _bookingApiService = bookingApiService;
        _servicesApiService = servicesApiService;
        _calendarApiService = calendarApiService;
        _session = session;

        Picker = new SlotPickerViewModel(calendarApiService);

        // The confirm bar's own state depends on what the picker holds, and the picker does not know about
        // booking. Re-evaluated on its every change rather than polled, so the button cannot lag the selection.
        Picker.SelectionChanged += (_, _) => NotifyDerived();
    }

    /// <summary>The provider whose catalogue and calendar drive this screen.</summary>
    private string ProviderEmail => _session.IsProvider ? _session.Email : CounterpartEmail;

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;
        await _session.RefreshAsync();

        try
        {
            var all = await _servicesApiService.GetServicesAsync(ProviderEmail);

            // Mirrors what the server will accept: an inactive service is not on offer, and an
            // unclassified one cannot be reached by a profession-first flow.
            var bookable = all
                .Where(service => service.IsActive && !string.IsNullOrWhiteSpace(service.ProfessionName));

            if (!string.IsNullOrWhiteSpace(ProfessionScope))
                bookable = bookable.Where(service =>
                    string.Equals(service.ProfessionName, ProfessionScope, StringComparison.OrdinalIgnoreCase));

            Services = bookable.OrderBy(service => service.Name, StringComparer.OrdinalIgnoreCase).ToList();

            // One service is not a choice — pick it so the customer goes straight to dates.
            if (Services.Count == 1)
            {
                SelectedService = Services[0];
                MarkSelectedService();
                await RefreshAvailabilityAsync();
            }
        }
        catch (Exception)
        {
            ErrorMessage = AppResources.GetString("Error_LoadProviderServices");
        }
        finally
        {
            IsLoading = false;
            NotifyDerived();
        }
    }

    [RelayCommand]
    private async Task SelectServiceAsync(ServiceItem? service)
    {
        SelectedService = service;
        MarkSelectedService();
        ErrorMessage = string.Empty;
        await RefreshAvailabilityAsync();
    }

    /// <summary>Keeps exactly one service card reading as chosen, including the auto-picked single service.</summary>
    private void MarkSelectedService()
    {
        foreach (var candidate in Services)
            candidate.IsSelected = ReferenceEquals(candidate, SelectedService);
    }

    /// <summary>
    /// Re-reads the selected service's availability. Deliberately does NOT clear
    /// <see cref="ErrorMessage"/>: the rejected-slot path sets a message and then refreshes, and clearing
    /// here wiped that message off the banner immediately after showing it.
    /// </summary>
    private async Task RefreshAvailabilityAsync()
    {
        Picker.Reset();
        NotifyDerived();

        if (SelectedService is null) return;

        // Sized to the chosen service, because slot boundaries depend on its duration: a 90-minute service has
        // strictly fewer valid starts than a 30-minute one.
        Picker.ProviderEmail = ProviderEmail;
        Picker.ServiceName = SelectedService.Name;

        await Picker.LoadAsync();

        // The picker words its own failure; this surfaces it on the page's one error banner so there is a single
        // place a customer looks for what went wrong.
        if (Picker.HasError) ErrorMessage = Picker.ErrorMessage;

        NotifyDerived();
    }

    [RelayCommand(CanExecute = nameof(CanBook))]
    private async Task BookAsync()
    {
        if (SelectedService is null || Picker.SelectedSlot is null) return;

        IsBooking = true;
        ErrorMessage = string.Empty;
        NotifyDerived();

        try
        {
            // The exact UTC instant the server offered, sent back unchanged — NOT the local rendering of
            // it. End comes from the service's own duration, so the booked length matches what was shown.
            var start = Picker.SelectedSlot.StartUtc;
            var minutes = SelectedService.DurationMinutes ?? DefaultDurationMinutes;
            var end = start.AddMinutes(minutes);

            var emailProvider = _session.IsProvider ? _session.Email : CounterpartEmail;
            var emailCustomer = _session.IsProvider ? CounterpartEmail : _session.Email;

            var identifier = await _bookingApiService.BookAppointmentAsync(
                emailProvider, emailCustomer, start, end, SelectedService.Name);

            if (identifier is null)
            {
                // Most likely someone took the slot between the fetch and the tap — the server rejects an
                // overlap. Re-fetch so the stale slot disappears instead of being offered again.
                ErrorMessage = AppResources.Error_BookingConflict;
                await ToastNotifier.ShowAsync(ErrorMessage);
                await RefreshAvailabilityAsync();
                return;
            }

            await ToastNotifier.ShowAsync(AppResources.GetString("Action_AppointmentBooked"));
            BookingSucceeded?.Invoke(this, identifier);
        }
        catch (GatewayServiceUnavailableException ex)
        {
            ErrorMessage = GatewayErrorMapper.Describe(ex.FailedService);
            await ToastNotifier.ShowAsync(ErrorMessage);
        }
        catch (HttpRequestException)
        {
            ErrorMessage = AppResources.Error_ServerUnavailable;
            await ToastNotifier.ShowAsync(ErrorMessage);
        }
        finally
        {
            IsBooking = false;
            NotifyDerived();
        }
    }

    private void NotifyDerived()
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(HasServices));
        OnPropertyChanged(nameof(HasNoServices));
        OnPropertyChanged(nameof(HasSelectedService));
        OnPropertyChanged(nameof(CanBook));
        OnPropertyChanged(nameof(SelectedSlotLabel));
        OnPropertyChanged(nameof(IsFullyBooked));
        OnPropertyChanged(nameof(SelectedServiceLabel));
        OnPropertyChanged(nameof(ConfirmPrompt));
        OnPropertyChanged(nameof(ShowConfirmPrompt));
        OnPropertyChanged(nameof(SummaryWith));
        OnPropertyChanged(nameof(SummaryService));
        OnPropertyChanged(nameof(SummaryPrice));
        OnPropertyChanged(nameof(SummaryDate));
        OnPropertyChanged(nameof(SummaryTimeRange));
        OnPropertyChanged(nameof(SummaryDuration));
        OnPropertyChanged(nameof(SummaryTimeZone));
        BookCommand.NotifyCanExecuteChanged();
    }

    partial void OnErrorMessageChanged(string value) => NotifyDerived();
    partial void OnIsLoadingChanged(bool value) => NotifyDerived();

    // Assigned from a Shell query property after the first binding pass, so the summary has to be told.
    partial void OnCounterpartNameChanged(string value) => NotifyDerived();
}
