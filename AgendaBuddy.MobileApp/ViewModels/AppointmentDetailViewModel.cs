using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgendaBuddy.Library.Entities;
using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Services;

namespace AgendaBuddy.MobileApp.ViewModels;

public enum ActionType
{
    Confirm,
    Cancel,
    Complete
}

public class AppointmentActionEventArgs : EventArgs
{
    public ActionType Action { get; }
    public AppointmentActionEventArgs(ActionType action) => Action = action;
}

public partial class AppointmentDetailViewModel : ObservableObject
{
    private readonly IBookingApiService _bookingApiService;
    private readonly IUserSessionService _session;

    [ObservableProperty] private AppointmentDetail? _appointment;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool _isConfirmEnabled = true;

    // ux-review.md 8-state spot-check, finding P3: the provider-view "mark complete" button needs an
    // explicit busy indicator for the new POST .../status call — the legacy PUT-based call this
    // replaces had no equivalent. Set only around the Completed transition (not Confirm/Cancel),
    // matching the Sign In button + ActivityIndicator overlay pattern already used on LoginPage.
    [ObservableProperty] private bool _isCompleting;

    [ObservableProperty] private bool _isCancelling;

    // Booking's GET/POST/PUT notes routes are all Provider-role-gated server-side
    // (OwnershipGuard.AssertRole(user, "Provider") in BookingModule.cs) — a Customer calling any of them gets
    // 403, so the section is hidden rather than shown-then-erroring.
    [ObservableProperty] private List<NoteEntity> _notes = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddNoteCommand))]
    private string _newNoteContent = string.Empty;
    [ObservableProperty] private bool _isLoadingNotes;
    [ObservableProperty] private string _notesErrorMessage = string.Empty;

    public bool ShowNotesSection => _session.IsProvider;
    public bool HasNotesError => !string.IsNullOrEmpty(NotesErrorMessage);

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool IsNotLoading => !IsLoading;
    public bool HasAppointment => Appointment is not null;

    // ux-review.md finding 3 / PRD requirement 6 / AC7: the customer-facing "mark complete" control must be
    // HIDDEN entirely, not disabled — a disabled button with no explanation invites "why can't I do this?"
    // Bound to the Complete button's IsVisible (not IsEnabled) in AppointmentDetailPage.xaml, and gates the
    // command's CanExecute below so the action is genuinely unavailable, not merely invisible.
    public bool ShowCompleteButton => _session.IsProvider;

    /// <summary>
    /// Confirming is the PROVIDER accepting the request. Hidden for a Customer, and hidden rather than
    /// disabled for the same reason as <see cref="ShowCompleteButton"/>. A customer could previously
    /// promote their own request straight to Booked, so "Booked" said nothing about whether the provider
    /// had agreed to it.
    /// </summary>
    public bool ShowConfirmButton => _session.IsProvider;

    /// <summary>
    /// Keeps the notes list out of the layout entirely when there are none. An empty CollectionView still
    /// claims its default height, which showed as a tall blank card and pushed the add-note field off-screen.
    /// </summary>
    public bool HasSessionNotes => Notes.Count > 0;

    /// <summary>Hides the phone line rather than leaving an empty row when no number was ever given.</summary>
    public bool HasContactPhone => !string.IsNullOrWhiteSpace(Appointment?.ContactPhone);

    /// <summary>
    /// Start time and how long the session runs, e.g. "10:00 AM · 45 min". The duration used to be the
    /// literal string "30 min" in XAML, so every appointment claimed 30 minutes however long it was booked
    /// for — a 45-minute session told both parties it was 30.
    /// </summary>
    public string TimeAndDurationLabel
    {
        get
        {
            if (Appointment is null) return string.Empty;
            var time = Appointment.ScheduledAt.ToString("h:mm tt");
            return Appointment.ServiceDurationMinutes is { } minutes
                ? $"{time} · {minutes} min"
                : time;
        }
    }

    // The Complete button itself, replaced by the busy indicator below while the status call is in
    // flight — matching LoginPage's Sign In button/ActivityIndicator overlay, not a new pattern.
    public bool ShowCompleteButtonIdle => ShowCompleteButton && !IsCompleting;

    public bool ShowCompletingIndicator => ShowCompleteButton && IsCompleting;

    public string AppointmentId { get; set; } = string.Empty;

    public event EventHandler<AppointmentActionEventArgs>? ActionRequested;

    public AppointmentDetailViewModel(IBookingApiService bookingApiService, IUserSessionService session)
    {
        _bookingApiService = bookingApiService;
        _session = session;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            var result = await _bookingApiService.GetAppointmentAsync(AppointmentId);
            if (result is null)
            {
                ErrorMessage = "Could not load appointment — try again.";
            }
            else
            {
                Appointment = result;
            }
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Could not load appointment — check your connection and try again.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LoadWithFallbackAsync(AppointmentDetail? fallback)
    {
        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            var result = await _bookingApiService.GetAppointmentAsync(AppointmentId);
            Appointment = result ?? fallback;

            // Fetched nothing and had nothing to fall back on. The page hides its whole body on
            // HasAppointment, so without a message this renders as a brand header over a blank screen — the
            // reader is told neither what happened nor what to do. Reached by a notification or push tap,
            // where the id is all that travels: most often a cancellation, whose appointment is hard-deleted.
            if (Appointment is null)
                ErrorMessage = "This appointment is no longer available — it may have been cancelled.";
        }
        catch (HttpRequestException)
        {
            Appointment = fallback;

            // A failed request is a different statement from "no longer available", and saying the wrong one
            // sends the reader to check the wrong thing.
            if (Appointment is null)
                ErrorMessage = "Could not load this appointment. Check your connection and try again.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void Confirm() =>
        ActionRequested?.Invoke(this, new AppointmentActionEventArgs(ActionType.Confirm));

    [RelayCommand]
    private void Cancel() =>
        ActionRequested?.Invoke(this, new AppointmentActionEventArgs(ActionType.Cancel));

    [RelayCommand(CanExecute = nameof(ShowCompleteButton))]
    private void Complete() =>
        ActionRequested?.Invoke(this, new AppointmentActionEventArgs(ActionType.Complete));

    /// <summary>
    /// The real cancellation path — <c>DELETE /api/v1/booking/appointments/</c> — replacing the previous
    /// (broken) attempt to reach <c>Cancelled</c> through the status-transition route, which only accepts
    /// <c>Booked</c>/<c>Completed</c> as a target.
    /// </summary>
    public async Task<bool> ExecuteCancelAsync()
    {
        if (Appointment is null)
            return false;

        IsLoading = true;
        IsCancelling = true;
        ErrorMessage = string.Empty;

        try
        {
            var cancelled = await _bookingApiService.CancelAppointmentAsync(
                AppointmentId, Appointment.ProviderEmail, Appointment.CustomerEmail);

            if (!cancelled)
            {
                ErrorMessage = "Could not cancel this appointment — try again.";
                await ToastNotifier.ShowAsync(ErrorMessage);
                return false;
            }

            Appointment.Status = AppointmentStatus.Cancelled;
            OnPropertyChanged(nameof(Appointment));
            await ToastNotifier.ShowAsync("Appointment cancelled.");
            return true;
        }
        catch (GatewayServiceUnavailableException ex)
        {
            ErrorMessage = GatewayErrorMapper.Describe(ex.FailedService);
            await ToastNotifier.ShowAsync(ErrorMessage);
            return false;
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Could not cancel this appointment — check your connection and try again.";
            await ToastNotifier.ShowAsync(ErrorMessage);
            return false;
        }
        finally
        {
            IsLoading = false;
            IsCancelling = false;
        }
    }

    [RelayCommand]
    private async Task LoadNotesAsync()
    {
        if (!ShowNotesSection)
            return;

        IsLoadingNotes = true;
        NotesErrorMessage = string.Empty;

        try
        {
            Notes = await _bookingApiService.GetNotesAsync(AppointmentId);
        }
        catch (Exception)
        {
            NotesErrorMessage = "Could not load notes. Check your connection and try again.";
        }
        finally
        {
            IsLoadingNotes = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanAddNote))]
    private async Task AddNoteAsync()
    {
        var content = NewNoteContent;
        NewNoteContent = string.Empty;

        try
        {
            var created = await _bookingApiService.CreateNoteAsync(AppointmentId, content);
            if (created is not null)
            {
                Notes = new List<NoteEntity>(Notes) { created };
                await ToastNotifier.ShowAsync("Note added.");
            }
            else
            {
                NotesErrorMessage = "Could not save the note. Check your connection and try again.";
                NewNoteContent = content;
                await ToastNotifier.ShowAsync(NotesErrorMessage);
            }
        }
        catch (Exception)
        {
            NotesErrorMessage = "Could not save the note. Check your connection and try again.";
            NewNoteContent = content;
            await ToastNotifier.ShowAsync(NotesErrorMessage);
        }
    }

    private bool CanAddNote() => !string.IsNullOrWhiteSpace(NewNoteContent);

    public async Task ExecuteStatusUpdateAsync(AppointmentStatus status)
    {
        IsLoading = true;
        var isCompleteTransition = status == AppointmentStatus.Completed;
        if (isCompleteTransition)
            IsCompleting = true;
        ErrorMessage = string.Empty;

        try
        {
            var updated = await _bookingApiService.UpdateStatusAsync(AppointmentId, status);
            if (updated is null)
            {
                // API returned non-success (e.g., 400 for invalid status).
                ErrorMessage = "Status update failed";
                await ToastNotifier.ShowAsync(ErrorMessage);
            }
            else
            {
                Appointment = updated;
                await ToastNotifier.ShowAsync($"Appointment {status.ToString().ToLowerInvariant()}.");
            }
        }
        catch (GatewayServiceUnavailableException ex)
        {
            // ux-review.md finding 2: name the failed cluster rather than a generic message.
            ErrorMessage = GatewayErrorMapper.Describe(ex.FailedService);
            await ToastNotifier.ShowAsync(ErrorMessage);
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Status update failed — check your connection and try again.";
            await ToastNotifier.ShowAsync(ErrorMessage);
        }
        finally
        {
            IsLoading = false;
            if (isCompleteTransition)
                IsCompleting = false;
        }
    }

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));

    partial void OnNotesErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasNotesError));

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsNotLoading));

    partial void OnIsCompletingChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowCompleteButtonIdle));
        OnPropertyChanged(nameof(ShowCompletingIndicator));
    }

    partial void OnNotesChanged(List<NoteEntity> value) => OnPropertyChanged(nameof(HasSessionNotes));

    partial void OnAppointmentChanged(AppointmentDetail? value)
    {
        OnPropertyChanged(nameof(HasAppointment));
        OnPropertyChanged(nameof(TimeAndDurationLabel));
        OnPropertyChanged(nameof(HasContactPhone));
    }
}
