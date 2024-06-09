namespace Library.Entities;

public class AppointmentEntity
{
    [BsonElement("date_time")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime Appointment { get; set; }

    [BsonElement("is_booked")] public bool IsBooked { get; set; } = false;
    [BsonElement("day_off")] public bool DayOff { get; set; } = false;

    public AppointmentEntity()
    {
    }

    public AppointmentEntity(DateTime appointment, bool isBooked, bool dayOff)
    {
        Appointment = appointment;
        IsBooked = isBooked;
        DayOff = dayOff;
    }
}