namespace AgendaBuddy.EventAndCommands.Events.Booking;

[ExcludeFromCodeCoverage]
public class BookAppointmentEvent : INotification
{
    public AppointmentEntity? AppointmentEntity { get; set; }
}
