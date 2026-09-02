using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Models;
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

    [ObservableProperty]
    private List<DateChoice> _bookableDates = new();

    [ObservableProperty]
    private DateOnly? _selectedDate;

    [ObservableProperty]
    private List<SlotChoice> _timesForSelectedDate = new();

    /// <summary>
    /// The chosen slot. Holds the server's own UTC instant, and renders through
    /// <see cref="AvailabilitySlot.Label"/> in the device's zone — the two must not be conflated.
    /// </summary>
    [ObservableProperty]
    private SlotChoice? _selectedSlot;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isLoadingAvailability;

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

    private ProviderAvailability _availability = ProviderAvailability.Empty;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool HasServices => Services.Count > 0;
    public bool HasNoServices => !IsLoading && Services.Count == 0;
    public bool HasSelectedService => SelectedService is not null;
    public bool HasBookableDates => BookableDates.Count > 0;
    public bool HasTimes => TimesForSelectedDate.Count > 0;
    public bool CanBook => SelectedService is not null && SelectedSlot is not null && !IsBooking;
    public bool HasSelectedSlot => SelectedSlot is not null;

    /// <summary>True once a service is chosen but the provider has no room at all in the window.</summary>
    public bool IsFullyBooked => HasSelectedService && !IsLoadingAvailability && BookableDates.Count == 0;

    public string SelectedServiceLabel => SelectedService is null
        ? "Choose a service"
        : $"{SelectedService.Name} · {SelectedService.DurationLabel}";

    /// <summary>The chosen slot on this device's clock — never the raw UTC value.</summary>
    public string SelectedSlotLabel => SelectedSlot is null
        ? string.Empty
        : $"Selected: {SelectedSlot.LocalStart:ddd d MMM, h:mm tt}";

    /// <summary>Prompt shown on the confirm bar before a slot is chosen, so the bar is never a bare button.</summary>
    public string ConfirmPrompt => SelectedService is null
        ? "Choose a service to see available times"
        : SelectedSlot is null ? "Choose a date and time" : string.Empty;

    public bool ShowConfirmPrompt => SelectedSlot is null;

    // ── Booking summary ───────────────────────────────────────────────────────────────────────────
    // Everything the customer is committing to, restated at the point of commitment. Each piece was
    // already on screen one step earlier; not repeating it here meant confirming a paid appointment on
    // the strength of a date and a time alone.

    public string SummaryWith => string.IsNullOrWhiteSpace(CounterpartName) ? CounterpartEmail : CounterpartName;

    public string SummaryService => SelectedService?.Name ?? string.Empty;

    public string SummaryPrice => SelectedService?.FeeLabel ?? string.Empty;

    /// <summary>Long-form date, e.g. "Saturday 5 September".</summary>
    public string SummaryDate => SelectedSlot is null ? string.Empty : $"{SelectedSlot.LocalStart:dddd d MMMM}";

    /// <summary>
    /// Start and end on this device's clock. The end is derived from the service's own duration — the same
    /// arithmetic the booking POST uses — so the window shown is the window booked.
    /// </summary>
    public string SummaryTimeRange
    {
        get
        {
            if (SelectedSlot is null || SelectedService is null) return string.Empty;
            var start = SelectedSlot.LocalStart;
            var end = start.AddMinutes(SelectedService.DurationMinutes ?? DefaultDurationMinutes);
            return $"{start:h:mm tt} – {end:h:mm tt}";
        }
    }

    public string SummaryDuration => SelectedService is null
        ? string.Empty
        : $"{SelectedService.DurationMinutes ?? DefaultDurationMinutes} min";

    /// <summary>
    /// Names the zone the times above are expressed in. A time with no zone is ambiguous the moment the
    /// customer and provider are not in the same one.
    /// </summary>
    public string SummaryTimeZone
    {
        get
        {
            if (SelectedSlot is null) return string.Empty;
            var zone = TimeZoneInfo.Local;
            return zone.IsDaylightSavingTime(SelectedSlot.LocalStart) ? zone.DaylightName : zone.StandardName;
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
            ErrorMessage = "Could not load this provider's services. Check your connection and try again.";
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
        var service = SelectedService;

        // Anything chosen under the previous service is meaningless now: its slot boundaries came from
        // that service's duration.
        SelectedDate = null;
        SelectedSlot = null;
        TimesForSelectedDate = [];
        BookableDates = [];
        _availability = ProviderAvailability.Empty;
        NotifyDerived();

        if (service is null) return;

        IsLoadingAvailability = true;

        try
        {
            _availability = await _calendarApiService.GetProviderAvailabilityAsync(
                ProviderEmail, service.Name, WindowDays);

            BookableDates = _availability.BookableDates.Select(date => new DateChoice(date)).ToList();

            // Land on the soonest date with room rather than today, which may well be full.
            if (_availability.FirstBookableDate is { } first)
                SelectDateOn(first);
        }
        catch (Exception)
        {
            ErrorMessage = "Could not load availability. Check your connection and try again.";
        }
        finally
        {
            IsLoadingAvailability = false;
            NotifyDerived();
        }
    }

    [RelayCommand]
    private void SelectDate(DateChoice? choice)
    {
        if (choice is not null) SelectDateOn(choice.Date);
    }

    private void SelectDateOn(DateOnly date)
    {
        SelectedDate = date;

        // Exactly one card reads as chosen. Driven off the collection rather than the tapped item so the
        // auto-selected soonest date highlights too, not only a date the customer tapped.
        foreach (var candidate in BookableDates)
            candidate.IsSelected = candidate.Date == date;

        // Read from the already-fetched window — switching dates never costs a request.
        TimesForSelectedDate = _availability.SlotsOn(date).Select(slot => new SlotChoice(slot)).ToList();

        // A slot from the previous date must not survive the change.
        SelectedSlot = null;
        NotifyDerived();
    }

    [RelayCommand]
    private void SelectSlot(SlotChoice? choice)
    {
        SelectedSlot = choice;

        foreach (var candidate in TimesForSelectedDate)
            candidate.IsSelected = ReferenceEquals(candidate, choice);

        NotifyDerived();
    }

    [RelayCommand(CanExecute = nameof(CanBook))]
    private async Task BookAsync()
    {
        if (SelectedService is null || SelectedSlot is null) return;

        IsBooking = true;
        ErrorMessage = string.Empty;
        NotifyDerived();

        try
        {
            // The exact UTC instant the server offered, sent back unchanged — NOT the local rendering of
            // it. End comes from the service's own duration, so the booked length matches what was shown.
            var start = SelectedSlot.StartUtc;
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
                ErrorMessage = "That time is no longer available. Pick another.";
                await ToastNotifier.ShowAsync(ErrorMessage);
                await RefreshAvailabilityAsync();
                return;
            }

            await ToastNotifier.ShowAsync("Appointment booked.");
            BookingSucceeded?.Invoke(this, identifier);
        }
        catch (GatewayServiceUnavailableException ex)
        {
            ErrorMessage = GatewayErrorMapper.Describe(ex.FailedService);
            await ToastNotifier.ShowAsync(ErrorMessage);
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Could not reach the server. Check your connection and try again.";
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
        OnPropertyChanged(nameof(HasBookableDates));
        OnPropertyChanged(nameof(HasTimes));
        OnPropertyChanged(nameof(CanBook));
        OnPropertyChanged(nameof(HasSelectedSlot));
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
    partial void OnIsLoadingAvailabilityChanged(bool value) => NotifyDerived();

    // Assigned from a Shell query property after the first binding pass, so the summary has to be told.
    partial void OnCounterpartNameChanged(string value) => NotifyDerived();
}
