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

    public void Book()
    {
        if (AppointmentStatus == AppointmentStatus.Requested)
            AppointmentStatus = AppointmentStatus.Booked;
        else
            throw new InvalidOperationException("Only requested appointments can be booked.");
    }

    public void Complete()
    {
        if (AppointmentStatus == AppointmentStatus.Booked)
            AppointmentStatus = AppointmentStatus.Completed;
        else
            throw new InvalidOperationException("Only booked appointments can be completed.");
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
        if (AppointmentStatus is AppointmentStatus.Requested or AppointmentStatus.Booked)
            AppointmentStatus = AppointmentStatus.Cancelled;
        else
            throw new InvalidOperationException(
                "Only requested or booked appointments can be cancelled.");
    }

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
            default:
                throw new InvalidOperationException(
                    $"'{target}' is not a state an appointment can be transitioned to. Legal targets are "
                    + "Booked, Completed and Cancelled.");
        }

        AppointmentDescription = EnumHelper<AppointmentStatus>.GetEnumDescription(AppointmentStatus);
    }
}
