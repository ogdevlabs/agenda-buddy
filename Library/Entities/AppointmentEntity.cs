namespace Library.Entities;

public class AppointmentEntity
{
    [BsonElement("email_provider")] public required string EmailProvider { get; set; } = string.Empty;
    [BsonElement("email_customer")] public required string EmailCustomer { get; set; } = string.Empty;

    [BsonElement("start")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime Start { get; set; }

    [BsonElement("end")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime End { get; set; }

    [BsonElement("is_booked")] public bool IsBooked { get; set; } = false;
    [BsonElement("day_off")] public bool DayOff { get; set; } = false;

    public AppointmentEntity()
    {
    }

    public AppointmentEntity(string emailProvider, string emailCustomer, DateTime start, DateTime end, bool isBooked, bool dayOff)
    {
        EmailProvider = emailProvider;
        EmailCustomer = emailCustomer;
        Start = start;
        End = end;
        IsBooked = isBooked;
        DayOff = dayOff;
    }
}