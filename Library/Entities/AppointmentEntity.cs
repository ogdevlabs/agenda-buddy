namespace Library.Entities;

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
        AppointmentStatus = AppointmentStatus.Requested;
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
}