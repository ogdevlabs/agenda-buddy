using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Resources.Strings;
using AgendaBuddy.MobileApp.Services;

namespace AgendaBuddy.MobileApp.ViewModels;

/// <summary>
/// "Pick one of this provider's actually-free times." Owns the date strip, the time chips and the selection.
/// </summary>
/// <remarks>
/// <para>
/// <b>Extracted because three flows need this and each would otherwise grow its own copy:</b> booking, a
/// provider rescheduling a session, and a customer proposing a new time. Three copies of a timezone-sensitive
/// picker is three places for the same off-by-one-day bug, and the interesting logic here is exactly the kind
/// that goes wrong quietly — <see cref="ProviderAvailability"/> groups by LOCAL date while holding UTC
/// instants, and getting that backwards files an evening slot under tomorrow for every reader behind UTC.
/// </para>
/// <para>
/// <b>Deliberately free of MAUI and of DI-resolved page state.</b> It takes only
/// <see cref="ICalendarApiService"/>, so it is constructible and assertable on the <c>net10.0</c> test slice
/// where none of the MAUI types exist — which is where every rule in here is actually covered.
/// </para>
/// <para>
/// It does not know what the chosen slot is FOR. Booking, rescheduling and proposing all differ in what they
/// do with a slot, not in how one is chosen, so the caller keeps that.
/// </para>
/// </remarks>
public partial class SlotPickerViewModel : ObservableObject
{
    private readonly ICalendarApiService _calendarApiService;

    /// <summary>How far ahead to offer. The server clamps to the same ceiling.</summary>
    public const int WindowDays = 90;

    /// <summary>Shown when a provider has no free time at all in the window.</summary>
    public const string FullyBookedMessage = "No free times in the next 90 days.";

    /// <summary>Shown when availability could not be read — a real failure, worded as one.</summary>
    public const string LoadFailedMessage = "Could not load available times. Check your connection and try again.";

    public SlotPickerViewModel(ICalendarApiService calendarApiService) =>
        _calendarApiService = calendarApiService;

    [ObservableProperty]
    private List<DateChoice> _bookableDates = [];

    [ObservableProperty]
    private DateOnly? _selectedDate;

    [ObservableProperty]
    private List<SlotChoice> _timesForSelectedDate = [];

    /// <summary>
    /// The chosen slot. Holds the server's own UTC instant, and renders through
    /// <see cref="AvailabilitySlot.Label"/> in the device's zone — the two must not be conflated.
    /// </summary>
    [ObservableProperty]
    private SlotChoice? _selectedSlot;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    private ProviderAvailability _availability = ProviderAvailability.Empty;

    /// <summary>Set once by the caller: whose calendar this offers, and for which service's length.</summary>
    public string ProviderEmail { get; set; } = string.Empty;

    public string? ServiceName { get; set; }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool HasBookableDates => BookableDates.Count > 0;
    public bool HasTimes => TimesForSelectedDate.Count > 0;
    public bool HasSelectedSlot => SelectedSlot is not null;

    /// <summary>
    /// The provider has been asked and has nothing free. Distinct from <see cref="HasError"/>: a full calendar
    /// is an answer, a failed request is not, and wording them the same makes a network blip look like a fully
    /// booked provider.
    /// </summary>
    public bool IsFullyBooked => !IsLoading && !HasError && BookableDates.Count == 0 && HasLoaded;

    /// <summary>Whether a load has completed, so "nothing free" is not claimed before anything was asked.</summary>
    public bool HasLoaded { get; private set; }

    /// <summary>The chosen slot on this device's clock — never the raw UTC value.</summary>
    public string SelectedSlotLabel => SelectedSlot is null
        ? string.Empty
        : SelectedSlot.LocalStart.ToString("ddd d MMM, t", AppResources.CurrentCulture);

    /// <summary>
    /// The instant to send back to the server, untouched. <c>null</c> until something is chosen.
    /// </summary>
    public DateTime? SelectedStartUtc => SelectedSlot?.StartUtc;

    /// <summary>
    /// Names the zone the times are expressed in. A time with no zone is ambiguous the moment the two parties
    /// are not in the same one, which for a reschedule they routinely are not.
    /// </summary>
    public string TimeZoneLabel
    {
        get
        {
            var reference = SelectedSlot?.LocalStart ?? DateTime.Now;
            var zone = TimeZoneInfo.Local;
            return zone.IsDaylightSavingTime(reference) ? zone.DaylightName : zone.StandardName;
        }
    }

    /// <summary>Raised whenever the selection changes, so a host can re-evaluate its own confirm button.</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>
    /// Reads the provider's availability for the whole window and lands on the soonest date with room.
    /// </summary>
    /// <remarks>
    /// Fetched ONCE and grouped by date, so moving between dates costs nothing. Re-called when the service
    /// changes, because slot boundaries depend on that service's duration — a 90-minute service has strictly
    /// fewer valid starts than a 30-minute one.
    /// </remarks>
    [RelayCommand]
    public async Task LoadAsync()
    {
        Reset();

        if (string.IsNullOrWhiteSpace(ProviderEmail)) return;

        IsLoading = true;
        NotifyDerived();

        try
        {
            _availability = await _calendarApiService.GetProviderAvailabilityAsync(
                ProviderEmail, ServiceName, WindowDays);

            BookableDates = _availability.BookableDates.Select(date => new DateChoice(date)).ToList();

            // Land on the soonest date with room rather than today, which may well be full.
            if (_availability.FirstBookableDate is { } first) SelectDateOn(first);
        }
        catch (Exception)
        {
            ErrorMessage = LoadFailedMessage;
        }
        finally
        {
            HasLoaded = true;
            IsLoading = false;
            NotifyDerived();
        }
    }

    /// <summary>
    /// Drops everything chosen and everything fetched.
    /// </summary>
    /// <remarks>
    /// Anything chosen under a previous service is meaningless: its slot boundaries came from that service's
    /// duration. Deliberately does NOT clear <see cref="ErrorMessage"/> — the rejected-slot path sets a message
    /// and then reloads, and clearing here wiped that message off the banner immediately after showing it.
    /// </remarks>
    public void Reset()
    {
        SelectedDate = null;
        SelectedSlot = null;
        TimesForSelectedDate = [];
        BookableDates = [];
        _availability = ProviderAvailability.Empty;
        HasLoaded = false;
        NotifyDerived();
    }

    [RelayCommand]
    private void SelectDate(DateChoice? choice)
    {
        if (choice is not null) SelectDateOn(choice.Date);
    }

    private void SelectDateOn(DateOnly date)
    {
        SelectedDate = date;

        // Exactly one card reads as chosen. Driven off the collection rather than the tapped item, so the
        // auto-selected soonest date highlights too — not only a date somebody tapped.
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

    /// <summary>
    /// Whether <paramref name="startUtc"/> is one of the slots currently on offer.
    /// </summary>
    /// <remarks>
    /// Availability at proposal time is not a reservation. A provider approving a customer's proposal has to
    /// re-check the slot is still free, and the server checks authoritatively — this is the client-side half, so
    /// the approve action can be withheld rather than offered and then refused.
    /// </remarks>
    public bool IsStillOnOffer(DateTime startUtc) =>
        _availability.SlotsByDate.Values.Any(slots => slots.Any(slot => slot.StartUtc == startUtc));

    private void NotifyDerived()
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(HasBookableDates));
        OnPropertyChanged(nameof(HasTimes));
        OnPropertyChanged(nameof(HasSelectedSlot));
        OnPropertyChanged(nameof(IsFullyBooked));
        OnPropertyChanged(nameof(HasLoaded));
        OnPropertyChanged(nameof(SelectedSlotLabel));
        OnPropertyChanged(nameof(SelectedStartUtc));
        OnPropertyChanged(nameof(TimeZoneLabel));
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnErrorMessageChanged(string value) => NotifyDerived();
    partial void OnIsLoadingChanged(bool value) => NotifyDerived();
}
