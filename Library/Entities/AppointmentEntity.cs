namespace Library.Entities;

public class AppointmentEntity
{
    [BsonElement("identifier")] public string Identifier { get; init; } = new Guid().ToString();

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

    [BsonElement("is_booked")] public bool IsBooked { get; set; } = false;
    [BsonElement("day_off")] public bool DayOff { get; set; } = false;

    [BsonElement("delivered")] public bool Delivered { get; set; } = false;

    public AppointmentEntity()
    {
    }

    public AppointmentEntity(string identifier, string emailProvider, string emailCustomer, DateTime start,
        DateTime end, bool isBooked, bool dayOff, bool delivered)
    {
        Identifier = identifier;
        EmailProvider = emailProvider;
        EmailCustomer = emailCustomer;
        Start = start;
        End = end;
        IsBooked = isBooked;
        DayOff = dayOff;
        Delivered = delivered;
    }
}