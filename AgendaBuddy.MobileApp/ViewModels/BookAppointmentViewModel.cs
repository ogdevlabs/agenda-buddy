using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Services;

namespace AgendaBuddy.MobileApp.ViewModels;

/// <summary>
/// Books a new appointment (<c>POST /api/v1/booking/appointments</c>) between the signed-in user and a
/// counterpart picked from <see cref="CustomersViewModel"/>'s contact list.
/// </summary>
/// <remarks>
/// <b>Deliberately no slot picker.</b> <c>GET /api/v1/calendar/availability/{email}</c> is
/// ownership-guarded to the CALLER'S OWN email (<c>OwnershipGuard.AssertOwner</c>) — a Customer has no route
/// that returns a Provider's free/busy times, and vice versa. Rather than fabricate an availability view
/// neither role can actually see, this lets either party propose a date/time directly; a Customer's proposal
/// lands as <c>AppointmentStatus.Requested</c> (the entity's own default), which the Provider then Confirms
/// or leaves — the same Requested→Booked flow <see cref="AppointmentDetailViewModel"/> already drives. See
/// agenda-buddy-e87's sibling gap: closing the provider-availability-for-customers hole is separate backend
/// work, not a mobile-only fix.
/// </remarks>
public partial class BookAppointmentViewModel : ObservableObject
{
    private readonly IBookingApiService _bookingApiService;
    private readonly IUserSessionService _session;

    [ObservableProperty]
    private DateTime _selectedDate = DateTime.Today.AddDays(1);

    [ObservableProperty]
    private TimeSpan _selectedTime = new(9, 0, 0);

    [ObservableProperty]
    private bool _isBooking;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public string CounterpartEmail { get; set; } = string.Empty;
    public string CounterpartName { get; set; } = string.Empty;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public event EventHandler<string>? BookingSucceeded;

    public BookAppointmentViewModel(IBookingApiService bookingApiService, IUserSessionService session)
    {
        _bookingApiService = bookingApiService;
        _session = session;
    }

    [RelayCommand]
    private async Task BookAsync()
    {
        IsBooking = true;
        ErrorMessage = string.Empty;

        try
        {
            var start = SelectedDate.Date + SelectedTime;
            var end = start.AddMinutes(30);

            if (start <= DateTime.Now)
            {
                ErrorMessage = "Pick a date and time in the future.";
                return;
            }

            var emailProvider = _session.IsProvider ? _session.Email : CounterpartEmail;
            var emailCustomer = _session.IsProvider ? CounterpartEmail : _session.Email;

            var identifier = await _bookingApiService.BookAppointmentAsync(emailProvider, emailCustomer, start, end);
            if (identifier is null)
            {
                ErrorMessage = "Could not book this appointment — try again.";
                await ToastNotifier.ShowAsync(ErrorMessage);
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
        }
    }

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));
}
