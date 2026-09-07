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
    Complete,

    /// <summary>The provider moving the session. Opens the slot picker.</summary>
    Reschedule,

    /// <summary>The customer asking for a new time. Opens the same slot picker.</summary>
    RequestNewTime,

    /// <summary>Accepting the outstanding proposal.</summary>
    ApproveReschedule,

    /// <summary>Refusing it. The session stays where it is.</summary>
    DeclineReschedule
}

public class AppointmentActionEventArgs : EventArgs
{
    public ActionType Action { get; }
    public AppointmentActionEventArgs(ActionType action) => Action = action;
}

/// <summary>
/// Which section of the appointment is on screen.
/// </summary>
/// <remarks>
/// The session's own facts — who, when, how long — stay visible above the tabs, because they are the identity of
/// the thing being looked at rather than one of its sections. What the tabs divide is what you can DO about it.
/// </remarks>
public enum AppointmentTab
{
    /// <summary>Reschedule, confirm, complete, cancel. The default, because it is why people open this page.</summary>
    Manage,

    /// <summary>Provider-only: the backend note routes are role-gated, so a customer has no tab here at all.</summary>
    Notes

    // Payment was a third tab and is gone: payments are being handled outside this screen, and a tab whose only
    // content restated the session's own facts was a section with nothing of its own to say.
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

    /// <summary>In flight for any of the four reschedule calls, so the action row can show it is working.</summary>
    [ObservableProperty] private bool _isRescheduling;

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

    // ── Tabs ──────────────────────────────────────────────────────────────────────────────────────────
    //
    // Three sections rather than one long column. Before this the page stacked the session facts, the
    // customer's note, a payment row, the provider's notes editor and a growing action row into a single
    // scroller -- which put the actions below the fold on a short screen and made the reason somebody opened
    // the page the hardest thing to reach.

    [ObservableProperty]
    private AppointmentTab _selectedTab = AppointmentTab.Manage;

    public bool IsManageTab => SelectedTab == AppointmentTab.Manage;

    /// <summary>
    /// Notes are shown only to a provider, and the TAB is hidden with them.
    /// </summary>
    /// <remarks>
    /// A tab that opens an empty section is worse than no tab: the backend note routes are Provider-role-gated,
    /// so a customer selecting it would be shown a section that can never hold anything.
    /// </remarks>
    public bool IsNotesTab => SelectedTab == AppointmentTab.Notes && ShowNotesSection;

    public bool ShowNotesTab => ShowNotesSection;

    /// <summary>
    /// How many of the strip's two columns the Manage tab occupies.
    /// </summary>
    /// <remarks>
    /// A customer has no Notes tab, so Manage spans both columns rather than sitting at half width beside an
    /// empty one — a hidden segment still reserves its column, which would leave the strip visibly lopsided for
    /// every customer.
    /// </remarks>
    public int ManageTabColumnSpan => ShowNotesTab ? 1 : 2;

    /// <summary>
    /// The Manage section: visible on its tab, and only once an appointment has loaded.
    /// </summary>
    /// <remarks>
    /// Gated on <see cref="HasAppointment"/> as well as the tab, because every action inside it reads the
    /// appointment's status and role — an empty card of hidden buttons is not something to render while a fetch
    /// is in flight.
    /// </remarks>
    public bool ShowManageSection => IsManageTab && HasAppointment;


    /// <summary>
    /// Moves to a tab by name, so the tab strip can be laid out declaratively.
    /// </summary>
    /// <remarks>
    /// An unrecognised name is ignored rather than defaulting to Manage: silently moving somebody off the tab
    /// they are on is more confusing than a tap that did nothing. Notes is refused for a customer, so the tab
    /// cannot be reached even if something asks for it.
    /// </remarks>
    [RelayCommand]
    private void SelectTab(string? tab)
    {
        if (!Enum.TryParse<AppointmentTab>(tab, ignoreCase: true, out var parsed)) return;
        if (parsed == AppointmentTab.Notes && !ShowNotesSection) return;

        SelectedTab = parsed;
    }
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
    public bool ShowConfirmButton => _session.IsProvider && Appointment?.Status == AppointmentStatus.Requested;

    // ── Reschedule ────────────────────────────────────────────────────────────────────────────────────
    //
    // The action row is state- AND role-dependent, and every gate below is HIDE rather than disable. A greyed
    // button with no explanation reads as broken, and a button that produces a server refusal is worse still:
    // an ungated affordance is what made a messaging 403 surface to the user as a connection failure.

    /// <summary>Only a booked session can be moved, and only its provider moves one outright.</summary>
    public bool ShowRescheduleButton =>
        _session.IsProvider && Appointment?.Status == AppointmentStatus.Booked;

    /// <summary>A customer asks rather than moves. Same status rule, other side of it.</summary>
    public bool ShowRequestNewTimeButton =>
        !_session.IsProvider && Appointment?.Status == AppointmentStatus.Booked;

    /// <summary>A proposal is outstanding on this appointment.</summary>
    public bool HasPendingReschedule => Appointment?.HasPendingReschedule == true;

    /// <summary>
    /// This reader made the outstanding proposal, so they are waiting rather than deciding.
    /// </summary>
    /// <remarks>
    /// The server refuses an answer from whoever proposed — otherwise asking and agreeing would be one act — so
    /// showing them Approve/Decline would be offering two buttons that both 409.
    /// </remarks>
    public bool IsMyPendingReschedule =>
        HasPendingReschedule
        && string.Equals(Appointment!.ProposedBy, _session.Email, StringComparison.OrdinalIgnoreCase);

    /// <summary>Approve and Decline, shown only to the party who owes the answer.</summary>
    public bool ShowRescheduleAnswerButtons => HasPendingReschedule && !IsMyPendingReschedule;

    /// <summary>The proposed time, on this device's clock.</summary>
    public string ProposedTimeLabel => Appointment?.ProposedStart is { } proposed
        ? $"{proposed:dddd d MMMM 'at' h:mm tt}"
        : string.Empty;

    /// <summary>What the pending banner says, which differs entirely by who is waiting on whom.</summary>
    public string PendingRescheduleMessage
    {
        get
        {
            if (!HasPendingReschedule) return string.Empty;

            return IsMyPendingReschedule
                ? $"You asked to move this to {ProposedTimeLabel}. Waiting for a reply — the session is still on "
                  + $"{Appointment!.ScheduledAt:dddd d MMMM 'at' h:mm tt} until then."
                : $"A new time was requested: {ProposedTimeLabel}. Until you answer, the session stays on "
                  + $"{Appointment!.ScheduledAt:dddd d MMMM 'at' h:mm tt}.";
        }
    }

    /// <summary>Set on a completed reschedule, so a move is visible as a move rather than read as the original.</summary>
    public bool WasRescheduled => Appointment?.PreviousStart is not null;

    public string PreviousTimeLabel => Appointment?.PreviousStart is { } previous
        ? $"Moved from {previous:ddd d MMM, h:mm tt}"
        : string.Empty;

    // ── Cancellation, and its notice period ───────────────────────────────────────────────────────────

    /// <summary>
    /// Whether cancelling is offered at all.
    /// </summary>
    /// <remarks>
    /// A PROVIDER may cancel at any notice. A CUSTOMER must give
    /// <see cref="AppointmentEntity.CustomerCancellationNoticeHours"/> hours, and inside that window the button is
    /// hidden rather than shown-and-refused. The gate reads the appointment already on screen, so it costs no
    /// request and is exactly as fresh as the card it sits on. The server still decides — this is courtesy, since
    /// the device clock is not trustworthy.
    /// </remarks>
    public bool ShowCancelButton
    {
        get
        {
            if (Appointment is null) return false;

            // Nothing to cancel once it is over or already called off.
            if (Appointment.Status is AppointmentStatus.Completed or AppointmentStatus.Cancelled) return false;

            return _session.IsProvider || DateTime.Now < Appointment.CustomerCancellationDeadline;
        }
    }

    /// <summary>
    /// Shown to a customer once the window has closed, in place of the button.
    /// </summary>
    /// <remarks>
    /// A missing button with no explanation is indistinguishable from a bug. The deadline is named as a concrete
    /// local time, because "too late" leaves the reader unable to tell how late.
    /// </remarks>
    public bool ShowCancellationClosedNotice =>
        Appointment is not null
        && !_session.IsProvider
        && Appointment.Status is not (AppointmentStatus.Completed or AppointmentStatus.Cancelled)
        && DateTime.Now >= Appointment.CustomerCancellationDeadline;

    public string CancellationClosedMessage =>
        $"Cancellations closed {Appointment?.CustomerCancellationDeadline:ddd d MMM 'at' h:mm tt}. "
        + "Message your provider to ask.";

    /// <summary>
    /// Shown to a customer while cancelling is still possible, so the deadline is not discovered by being
    /// refused.
    /// </summary>
    public bool ShowCancellationDeadlineNotice =>
        ShowCancelButton && !_session.IsProvider;

    public string CancellationDeadlineMessage =>
        $"Free to cancel until {Appointment?.CustomerCancellationDeadline:ddd d MMM, h:mm tt}.";

    /// <summary>
    /// Keeps the notes list out of the layout entirely when there are none. An empty CollectionView still
    /// claims its default height, which showed as a tall blank card and pushed the add-note field off-screen.
    /// </summary>
    public bool HasSessionNotes => Notes.Count > 0;

    /// <summary>
    /// Nothing recorded yet — said out loud, because a notes tab that renders nothing looks broken.
    /// </summary>
    /// <remarks>
    /// Not claimed while the read is still in flight: "no notes" and "not asked yet" are different statements.
    /// </remarks>
    public bool HasNoSessionNotes => !IsLoadingNotes && Notes.Count == 0 && !HasNotesError;

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
            // reader is told neither what happened nor what to do. Reached by a notification or push tap, where
            // the id is all that travels. Not a cancellation: cancelling is a soft delete, so a cancelled
            // appointment still loads and shows its Cancelled status.
            if (Appointment is null)
                ErrorMessage = "This appointment could not be found. It may have been removed.";
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

    [RelayCommand(CanExecute = nameof(ShowRescheduleButton))]
    private void Reschedule() =>
        ActionRequested?.Invoke(this, new AppointmentActionEventArgs(ActionType.Reschedule));

    [RelayCommand(CanExecute = nameof(ShowRequestNewTimeButton))]
    private void RequestNewTime() =>
        ActionRequested?.Invoke(this, new AppointmentActionEventArgs(ActionType.RequestNewTime));

    [RelayCommand(CanExecute = nameof(ShowRescheduleAnswerButtons))]
    private void ApproveReschedule() =>
        ActionRequested?.Invoke(this, new AppointmentActionEventArgs(ActionType.ApproveReschedule));

    [RelayCommand(CanExecute = nameof(ShowRescheduleAnswerButtons))]
    private void DeclineReschedule() =>
        ActionRequested?.Invoke(this, new AppointmentActionEventArgs(ActionType.DeclineReschedule));

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

            if (!cancelled.Succeeded)
            {
                // The server's own wording, which for the case a customer will actually hit names the
                // cancellation deadline. Only the server holds the authoritative clock, so this cannot be
                // reconstructed here -- and "try again" would be advice that never works.
                ErrorMessage = cancelled.ErrorMessage ?? "Could not cancel this appointment — try again.";
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

    /// <summary>
    /// Applies a chosen slot: the provider moves the session, a customer proposes it.
    /// </summary>
    /// <remarks>
    /// One method for both, branching on the session role, because the two differ only in which route is called
    /// and what is said afterwards. The instant passed in is the server's own UTC value from the availability
    /// response, unchanged — sending a local rendering of it would book a different time than the one shown.
    /// </remarks>
    public async Task<bool> ExecuteRescheduleAsync(DateTime newStartUtc)
    {
        if (Appointment is null) return false;

        IsLoading = true;
        IsRescheduling = true;
        ErrorMessage = string.Empty;

        try
        {
            var result = _session.IsProvider
                ? await _bookingApiService.RescheduleAsync(AppointmentId, newStartUtc)
                : await _bookingApiService.RequestRescheduleAsync(AppointmentId, newStartUtc);

            if (!result.Succeeded)
            {
                ErrorMessage = result.ErrorMessage ?? "Could not change this appointment. Try again.";
                await ToastNotifier.ShowAsync(ErrorMessage);
                return false;
            }

            await ToastNotifier.ShowAsync(_session.IsProvider
                ? "Session rescheduled. The customer has been notified."
                : "New time requested. Your provider will answer.");

            // Re-read rather than patching in memory: the server decides where the session ended up and whether
            // a proposal is now outstanding, and guessing either would put the page out of step with the truth.
            await LoadAsync();
            return true;
        }
        catch (GatewayServiceUnavailableException ex)
        {
            ErrorMessage = GatewayErrorMapper.Describe(ex.FailedService);
            await ToastNotifier.ShowAsync(ErrorMessage);
            return false;
        }
        finally
        {
            IsLoading = false;
            IsRescheduling = false;
        }
    }

    /// <summary>
    /// Answers the outstanding proposal.
    /// </summary>
    /// <remarks>
    /// A refused APPROVAL is a normal outcome, not an error: the slot may have been taken between the request and
    /// the answer. It is worded from the server's message and the page reloads, so the stale proposal disappears
    /// rather than being offered again.
    /// </remarks>
    public async Task<bool> ExecuteRescheduleAnswerAsync(bool approve)
    {
        if (Appointment is null) return false;

        IsLoading = true;
        IsRescheduling = true;
        ErrorMessage = string.Empty;

        try
        {
            var result = await _bookingApiService.AnswerRescheduleAsync(AppointmentId, approve);

            if (!result.Succeeded)
            {
                ErrorMessage = result.ErrorMessage
                    ?? "Could not answer this request. It may have been withdrawn.";
                await ToastNotifier.ShowAsync(ErrorMessage);
                await LoadAsync();
                return false;
            }

            await ToastNotifier.ShowAsync(approve
                ? "New time approved. The session has moved."
                : "New time declined. The session stays as it was.");

            await LoadAsync();
            return true;
        }
        catch (GatewayServiceUnavailableException ex)
        {
            ErrorMessage = GatewayErrorMapper.Describe(ex.FailedService);
            await ToastNotifier.ShowAsync(ErrorMessage);
            return false;
        }
        finally
        {
            IsLoading = false;
            IsRescheduling = false;
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

    partial void OnSelectedTabChanged(AppointmentTab value)
    {
        OnPropertyChanged(nameof(IsManageTab));
        OnPropertyChanged(nameof(IsNotesTab));
        OnPropertyChanged(nameof(ShowManageSection));
    }

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));

    partial void OnNotesErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasNotesError));
        OnPropertyChanged(nameof(HasNoSessionNotes));
    }

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsNotLoading));

    partial void OnIsCompletingChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowCompleteButtonIdle));
        OnPropertyChanged(nameof(ShowCompletingIndicator));
    }

    partial void OnNotesChanged(List<NoteEntity> value)
    {
        OnPropertyChanged(nameof(HasSessionNotes));
        OnPropertyChanged(nameof(HasNoSessionNotes));
    }

    partial void OnIsLoadingNotesChanged(bool value) => OnPropertyChanged(nameof(HasNoSessionNotes));

    partial void OnAppointmentChanged(AppointmentDetail? value)
    {
        OnPropertyChanged(nameof(HasAppointment));
        OnPropertyChanged(nameof(TimeAndDurationLabel));
        OnPropertyChanged(nameof(HasContactPhone));
        OnPropertyChanged(nameof(ShowManageSection));

        // Every action's visibility depends on the appointment's status and on who is reading, so all of them
        // have to be re-raised here. A missed one leaves the row showing the PREVIOUS appointment's affordances
        // -- which after a reschedule means offering to reschedule a session that just moved.
        OnPropertyChanged(nameof(ShowConfirmButton));
        OnPropertyChanged(nameof(ShowRescheduleButton));
        OnPropertyChanged(nameof(ShowRequestNewTimeButton));
        OnPropertyChanged(nameof(HasPendingReschedule));
        OnPropertyChanged(nameof(IsMyPendingReschedule));
        OnPropertyChanged(nameof(ShowRescheduleAnswerButtons));
        OnPropertyChanged(nameof(ProposedTimeLabel));
        OnPropertyChanged(nameof(PendingRescheduleMessage));
        OnPropertyChanged(nameof(WasRescheduled));
        OnPropertyChanged(nameof(PreviousTimeLabel));
        OnPropertyChanged(nameof(ShowCancelButton));
        OnPropertyChanged(nameof(ShowCancellationClosedNotice));
        OnPropertyChanged(nameof(CancellationClosedMessage));
        OnPropertyChanged(nameof(ShowCancellationDeadlineNotice));
        OnPropertyChanged(nameof(CancellationDeadlineMessage));

        RescheduleCommand.NotifyCanExecuteChanged();
        RequestNewTimeCommand.NotifyCanExecuteChanged();
        ApproveRescheduleCommand.NotifyCanExecuteChanged();
        DeclineRescheduleCommand.NotifyCanExecuteChanged();
    }
}
