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
    /// Moves this appointment to <paramref name="target"/> through the transition rules, and refreshes the
    /// human-readable description to match.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The transition is not legal from the current status, or <paramref name="target"/> is not a state any
    /// transition may reach.
    /// </exception>
    /// <remarks>
    /// <para>
    /// F-014 requirement 14 / threat T-203. <b>Until this existed, the rules above were dead code.</b>
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
    /// <c>Confirmed</c> and <c>Cancelled</c> are deliberately not reachable. <c>Confirmed</c> is only ever
    /// produced on a Calendar projection, and <c>Cancelled</c> is never persisted because cancellation
    /// deletes the document. Adding them is a product question about what those states mean, not a wiring
    /// gap — see `api-contracts.md` §3.
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
            default:
                throw new InvalidOperationException(
                    $"'{target}' is not a state an appointment can be transitioned to. Legal targets are "
                    + "Booked and Completed.");
        }

        AppointmentDescription = EnumHelper<AppointmentStatus>.GetEnumDescription(AppointmentStatus);
    }
}
