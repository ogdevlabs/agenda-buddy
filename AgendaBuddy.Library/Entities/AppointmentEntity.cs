namespace AgendaBuddy.Library.Entities;

[ExcludeFromCodeCoverage]
public class AppointmentEntity
{
    public AppointmentEntity()

    {
    }

    public AppointmentEntity(string identifier, string emailProvider, string emailCustomer, DateTime start,
        DateTime end, bool dayOff, AppointmentStatus appointmentStatus = AppointmentStatus.Requested)
    {
        Identifier = identifier;
        EmailProvider = emailProvider;
        EmailCustomer = emailCustomer;
        Start = start;
        End = end;
        DayOff = dayOff;
        AppointmentStatus = appointmentStatus;
    }

    [BsonElement("_id")] public ObjectId Id { get; set; }
    [BsonElement("identifier")] public string Identifier { get; init; } = Guid.NewGuid().ToString();

    [BsonElement("email_provider")]
    [EmailAddress]
    public required string EmailProvider { get; set; } = string.Empty;

    [BsonElement("email_customer")]
    [EmailAddress]
    public required string EmailCustomer { get; set; } = string.Empty;

    [BsonElement("start")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime Start { get; set; }

    [BsonElement("end")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime End { get; set; }

    [BsonElement("appointment_status")]
    public AppointmentStatus AppointmentStatus { get; set; } = AppointmentStatus.Requested;

    [BsonElement("appointment_description")]
    public string AppointmentDescription { get; set; } =
        EnumHelper<AppointmentStatus>.GetEnumDescription(AppointmentStatus.Requested);

    [BsonElement("day_off")] public bool DayOff { get; set; }

    /// <summary>
    /// The provider's service this session is for, by name — the same key <c>ServiceEntity.Name</c> is
    /// matched on everywhere else (PUT/PATCH/DELETE on services all match by name, because
    /// <c>ServiceEntity.Id</c> is unusable over the wire, see <c>ObjectIdJsonConverter</c>'s remarks).
    /// </summary>
    /// <remarks>
    /// Additive (2026-08-29). Null on every appointment booked before a service could be chosen, so it
    /// stays nullable rather than required — a historical appointment genuinely has no service, and
    /// backfilling one would be inventing data. <c>AppointmentDetail</c>/<c>AppointmentSummary</c> on the
    /// client already carried <c>ServiceName</c> with nothing to populate it; this is what populates it.
    /// </remarks>
    [BsonElement("service_name")]
    [BsonIgnoreIfNull]
    public string? ServiceName { get; set; }

    /// <summary>
    /// Session length in minutes as it was when booked, so a later edit to the service does not
    /// retroactively change what was agreed. Null for appointments booked before services were selectable.
    /// </summary>
    [BsonElement("service_duration_minutes")]
    [BsonIgnoreIfNull]
    public int? ServiceDurationMinutes { get; set; }

    [BsonElement("service_fee")]
    [BsonIgnoreIfNull]
    public decimal? ServiceFee { get; set; }

    [BsonElement("service_fee_type")]
    [BsonIgnoreIfNull]
    public FeeType? ServiceFeeType { get; set; }

    [BsonElement("payment_amount_minor")]
    [BsonIgnoreIfNull]
    public long? PaymentAmountMinor { get; set; }

    [BsonElement("payment_currency")]
    [BsonIgnoreIfNull]
    public string? PaymentCurrency { get; set; }

    [BsonElement("pricing_version")]
    [BsonIgnoreIfNull]
    public int? PricingVersion { get; set; }

    /// <summary>
    /// The new start time one party has proposed, UTC. Set only while
    /// <see cref="AppointmentStatus.RescheduleRequested"/>; cleared on approval and on decline.
    /// </summary>
    /// <remarks>
    /// Held alongside <see cref="Start"/> rather than overwriting it, because a proposal is not an agreement:
    /// until the other party answers, the session is still at its original time and every calendar must keep
    /// showing it there.
    /// </remarks>
    [BsonElement("proposed_start")]
    [BsonIgnoreIfNull]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? ProposedStart { get; set; }

    /// <summary>
    /// The email of whoever proposed <see cref="ProposedStart"/>. Cleared with it.
    /// </summary>
    /// <remarks>
    /// An email rather than a role, because the email is the identity everywhere else — it is what
    /// <c>OwnershipGuard</c> authorises off. Compare it against <see cref="EmailCustomer"/> to decide which
    /// side is waiting on an answer; storing a role as well would be a second copy of that fact, free to
    /// disagree with the first.
    /// </remarks>
    [BsonElement("proposed_by")]
    [BsonIgnoreIfNull]
    public string? ProposedBy { get; set; }

    /// <summary>
    /// Where this appointment was before the most recent completed reschedule, UTC.
    /// </summary>
    /// <remarks>
    /// Kept so a reschedule can be described rather than merely applied — "moved from Tuesday 3pm" needs the
    /// old time, and without it a reschedule is indistinguishable from an appointment that was always at the
    /// new time. Only the most recent is retained; a full history would be an audit concern, and the audit
    /// <c>EventStore</c> already holds one.
    /// </remarks>
    [BsonElement("previous_start")]
    [BsonIgnoreIfNull]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? PreviousStart { get; set; }

    /// <summary>
    /// How much notice a CUSTOMER must give to cancel. A provider may cancel at any notice.
    /// </summary>
    /// <remarks>
    /// The asymmetry is the point: a provider calling off a session they cannot make is unavoidable, while a
    /// customer cancelling an hour beforehand costs the provider a slot nobody else can now take.
    /// </remarks>
    public const int CustomerCancellationNoticeHours = 24;

    /// <summary>
    /// The last instant a customer may still cancel this appointment.
    /// </summary>
    /// <remarks>
    /// Exposed so the deadline can be SHOWN while it is still in the future, rather than discovered by being
    /// refused. Computed from the appointment's own start, so it is stable regardless of when it is read.
    /// </remarks>
    /// <remarks>
    /// Clamped rather than computed blindly. A row whose <see cref="Start"/> is <c>default</c> — which older
    /// documents and hand-built fixtures both have — would otherwise underflow subtracting the notice period, and
    /// an <c>ArgumentOutOfRangeException</c> out of a property getter is a crash on a read path. Such an
    /// appointment has no meaningful deadline, and <see cref="DateTime.MinValue"/> is the answer that makes it
    /// uncancellable-by-notice rather than freely cancellable.
    /// </remarks>
    [BsonIgnore]
    public DateTime CustomerCancellationDeadlineUtc
    {
        get
        {
            var start = Start.ToUniversalTime();
            var notice = TimeSpan.FromHours(CustomerCancellationNoticeHours);
            return start - DateTime.MinValue < notice ? DateTime.MinValue : start - notice;
        }
    }

    /// <summary>Whether a customer may still cancel, as of <paramref name="nowUtc"/>.</summary>
    public bool CustomerMayCancelAt(DateTime nowUtc) => nowUtc < CustomerCancellationDeadlineUtc;

    /// <summary>
    /// Whether this session is over and should be recorded as completed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Completion is a fact about the clock, not a claim somebody makes.</b> It used to be a button the
    /// provider pressed, which made "Completed" a chore they had to remember — and a session nobody marked stayed
    /// <c>Booked</c> for ever, so <c>ReportingService</c> counted it as outstanding indefinitely.
    /// </para>
    /// <para>
    /// <c>Booked</c> qualifies, and so does <c>RescheduleRequested</c>: a session whose time has passed happened
    /// at the time it was booked for, so a proposal to move it that nobody answered in time is moot. Leaving it
    /// alone was the one state auto-completion could not resolve, and it would have sat in the provider's
    /// outstanding count for ever waiting on an answer that no longer means anything.
    /// </para>
    /// <para>
    /// <c>Requested</c> does not: it was never agreed to, so a past request is an expired ask rather than work
    /// delivered. Neither does <c>Cancelled</c>, which was called off.
    /// </para>
    /// </remarks>
    public bool ShouldAutoCompleteAt(DateTime nowUtc) =>
        AppointmentStatus is AppointmentStatus.Booked or AppointmentStatus.RescheduleRequested
        && EffectiveEndUtc <= nowUtc;

    /// <summary>
    /// When this session actually finishes, UTC.
    /// </summary>
    /// <remarks>
    /// Older rows predate <c>End</c> being required and can carry one at or before <c>Start</c>; those are treated
    /// as one default-length session rather than as zero-length, which would complete them the moment they began.
    /// </remarks>
    [BsonIgnore]
    public DateTime EffectiveEndUtc
    {
        get
        {
            var start = AsUtc(Start);
            var end = AsUtc(End);
            return end > start
                ? end
                : start.AddMinutes(ServiceDurationMinutes is > 0 ? ServiceDurationMinutes.Value : 60);
        }
    }

    /// <summary>
    /// Reads a stored instant as UTC, whatever <see cref="DateTimeKind"/> it arrives with.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b><c>ToUniversalTime()</c> is wrong for an <c>Unspecified</c> value: it treats it as LOCAL and shifts
    /// it by the machine's offset.</b> <c>start</c> and <c>end</c> are stored UTC — that is what
    /// <c>[BsonDateTimeOptions(Kind = DateTimeKind.Utc)]</c> declares — so an unspecified kind means "already UTC,
    /// just untagged", which is what a hand-built object or a future code path can hand in. Interpreting it as
    /// local made this rule depend on the timezone the process happened to run in, which is exactly the class of
    /// defect <c>AvailabilityCalculator</c> was rewritten to remove.
    /// </remarks>
    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        _ => value.ToUniversalTime()
    };

    /// <summary>Whether a reschedule proposal is outstanding on this appointment.</summary>
    [BsonIgnore]
    public bool HasPendingReschedule =>
        AppointmentStatus == AppointmentStatus.RescheduleRequested && ProposedStart.HasValue;

    /// <summary>Whether the outstanding proposal came from the customer, so the provider owes the answer.</summary>
    [BsonIgnore]
    public bool ProposedByCustomer =>
        ProposedBy is not null
        && string.Equals(ProposedBy, EmailCustomer, StringComparison.OrdinalIgnoreCase);

    public void Book()
    {
        if (AppointmentStatus == AppointmentStatus.Requested)
            AppointmentStatus = AppointmentStatus.Booked;
        else
            throw new InvalidOperationException("Only requested appointments can be booked.");
    }

    /// <summary>
    /// Marks the session as delivered.
    /// </summary>
    /// <remarks>
    /// <b>An unanswered reschedule proposal does not block completion.</b> A session whose time has passed
    /// happened at the time it was booked for, so a proposal to move it is moot — and leaving it
    /// <c>RescheduleRequested</c> for ever was the one state auto-completion could not resolve, so it sat in the
    /// provider's outstanding count indefinitely waiting for an answer that no longer means anything. The
    /// proposal is cleared with it, because there is nothing left to move.
    /// </remarks>
    public void Complete()
    {
        if (AppointmentStatus is AppointmentStatus.Booked or AppointmentStatus.RescheduleRequested)
        {
            AppointmentStatus = AppointmentStatus.Completed;
            ClearProposal();
        }
        else
        {
            throw new InvalidOperationException("Only booked appointments can be completed.");
        }
    }

    /// <summary>
    /// Moves a requested or booked appointment to <see cref="AppointmentStatus.Cancelled"/>.
    /// </summary>
    /// <remarks>
    /// Either party may cancel, and both <c>Requested</c> and <c>Booked</c> are cancellable — a customer
    /// withdrawing a request and a provider calling off a confirmed session are the same operation as far as
    /// the record goes. <c>Completed</c> is not: it is history, and "cancel" is not a thing you can do to work
    /// that was already delivered. That guard already existed in the cancel handler; it lives here now, with
    /// the other two transitions.
    /// </remarks>
    public void Cancel()
    {
        // An appointment with a reschedule proposal outstanding is still cancellable, and has to be: otherwise
        // a pending proposal would trap both parties until somebody answered it. Cancelling drops the proposal
        // with it — there is nothing left to move.
        if (AppointmentStatus is AppointmentStatus.Requested
            or AppointmentStatus.Booked
            or AppointmentStatus.RescheduleRequested)
        {
            AppointmentStatus = AppointmentStatus.Cancelled;
            ClearProposal();
        }
        else
        {
            throw new InvalidOperationException(
                "Only requested or booked appointments can be cancelled.");
        }
    }

    /// <summary>
    /// Records a proposal to move this appointment to <paramref name="proposedStart"/>, without moving it.
    /// </summary>
    /// <param name="proposedStart">The new start, UTC. Must be in the future and different from the current one.</param>
    /// <param name="proposedBy">The email of the party proposing it.</param>
    /// <param name="nowUtc">
    /// The instant to treat as now, passed in rather than read from the clock so the rule is testable and so
    /// the caller's authoritative time is the one that applies.
    /// </param>
    /// <remarks>
    /// <b>Only a <see cref="AppointmentStatus.Booked"/> appointment can be rescheduled.</b> A
    /// <see cref="AppointmentStatus.Requested"/> one has not been agreed to yet — there is nothing to move, and
    /// the customer can simply cancel and request another time. <see cref="AppointmentStatus.Completed"/> is
    /// history. Proposing a second time while one is already outstanding is refused rather than silently
    /// replacing the first, so neither party answers a proposal the other has already withdrawn.
    /// </remarks>
    public void RequestReschedule(DateTime proposedStart, string proposedBy, DateTime nowUtc)
    {
        // Blank is rejected as well as null: a proposal nobody is recorded as having made cannot be answered,
        // because there is no way to tell which party owes the answer.
        ArgumentException.ThrowIfNullOrWhiteSpace(proposedBy);

        if (AppointmentStatus != AppointmentStatus.Booked)
        {
            throw new InvalidOperationException(
                AppointmentStatus == AppointmentStatus.RescheduleRequested
                    ? "This appointment already has a reschedule request awaiting an answer."
                    : "Only booked appointments can be rescheduled.");
        }

        var proposed = DateTime.SpecifyKind(proposedStart, DateTimeKind.Utc);

        if (proposed <= nowUtc)
            throw new InvalidOperationException("A reschedule must propose a time in the future.");

        if (proposed == Start.ToUniversalTime())
            throw new InvalidOperationException("The proposed time is the time already booked.");

        ProposedStart = proposed;
        ProposedBy = proposedBy;
        AppointmentStatus = AppointmentStatus.RescheduleRequested;
        Describe();
    }

    /// <summary>
    /// Accepts the outstanding proposal: the session moves, its old start is retained, and it is booked again.
    /// </summary>
    /// <remarks>
    /// The session's LENGTH is preserved rather than recomputed, so approving a move cannot quietly shorten or
    /// extend what was agreed. <c>End</c> follows <c>Start</c> by the same span it did before.
    /// </remarks>
    public void ApproveReschedule()
    {
        if (AppointmentStatus != AppointmentStatus.RescheduleRequested || !ProposedStart.HasValue)
            throw new InvalidOperationException("There is no reschedule request to approve.");

        var start = Start.ToUniversalTime();
        var end = End.ToUniversalTime();
        var length = end > start ? end - start : TimeSpan.FromMinutes(ServiceDurationMinutes ?? 60);

        PreviousStart = start;
        Start = ProposedStart.Value;
        End = ProposedStart.Value.Add(length);
        AppointmentStatus = AppointmentStatus.Booked;
        ClearProposal();
        Describe();
    }

    /// <summary>
    /// Refuses the outstanding proposal. The appointment stays exactly where it was and is booked again.
    /// </summary>
    public void DeclineReschedule()
    {
        if (AppointmentStatus != AppointmentStatus.RescheduleRequested)
            throw new InvalidOperationException("There is no reschedule request to decline.");

        AppointmentStatus = AppointmentStatus.Booked;
        ClearProposal();
        Describe();
    }

    private void ClearProposal()
    {
        ProposedStart = null;
        ProposedBy = null;
    }

    private void Describe() =>
        AppointmentDescription = EnumHelper<AppointmentStatus>.GetEnumDescription(AppointmentStatus);

    /// <summary>
    /// Moves this appointment to <paramref name="target"/> through the transition rules, and refreshes the
    /// human-readable description to match.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The transition is not legal from the current status, or <paramref name="target"/> is not a state any
    /// transition may reach.
    /// </exception>
    /// <remarks>
    /// <para>
    /// <b>Until this existed, the rules above were dead code.</b>
    /// Nothing in production called <see cref="Book"/> or <see cref="Complete"/>; what ran instead was
    /// <c>appointment.AppointmentStatus = appointmentEntity.AppointmentStatus</c> in
    /// <c>UpdateAppointmentCommandHandler</c> — the client's value, copied in, with the guards bypassed. A
    /// customer could mark a brand-new appointment <c>Completed</c>, which is a claim that work was
    /// delivered.
    /// </para>
    /// <para>
    /// <b>This routes through the two existing methods rather than reimplementing the table</b> (ADR D-4):
    /// the invariant stays in one place, and a state added to <see cref="AppointmentStatus"/> without a
    /// method is unreachable by construction rather than silently permitted.
    /// </para>
    /// <para>
    /// <c>Confirmed</c> remains deliberately unreachable: it is only ever produced on a Calendar projection,
    /// never persisted.
    /// </para>
    /// <para>
    /// <c>Cancelled</c> IS reachable now. Cancellation used to hard-delete the appointment document and drop it
    /// from the provider's embedded list, which meant the state existed in the enum and was never written —
    /// and that a cancelled appointment left no record that the slot had ever been booked. It is now a soft
    /// delete through <see cref="Cancel"/>.
    /// </para>
    /// <para>
    /// <b><c>RescheduleRequested</c> is deliberately NOT reachable from here</b>, and that is a guard rather
    /// than an omission. A proposal is meaningless without the time being proposed and the party proposing it,
    /// so it cannot be expressed as a bare status change — allowing one would let a caller declare a pending
    /// reschedule carrying no proposed time, which every reader would then have to defend against. Use
    /// <see cref="RequestReschedule"/>, <see cref="ApproveReschedule"/> and <see cref="DeclineReschedule"/>.
    /// </para>
    /// </remarks>
    public void TransitionTo(AppointmentStatus target)
    {
        switch (target)
        {
            case AppointmentStatus.Booked:
                Book();
                break;
            case AppointmentStatus.Completed:
                Complete();
                break;
            case AppointmentStatus.Cancelled:
                Cancel();
                break;
            case AppointmentStatus.RescheduleRequested:
                throw new InvalidOperationException(
                    "A reschedule request carries a proposed time and the party proposing it, so it cannot be "
                    + "set as a bare status. Use RequestReschedule, ApproveReschedule or DeclineReschedule.");
            default:
                throw new InvalidOperationException(
                    $"'{target}' is not a state an appointment can be transitioned to. Legal targets are "
                    + "Booked, Completed and Cancelled.");
        }

        Describe();
    }
}
