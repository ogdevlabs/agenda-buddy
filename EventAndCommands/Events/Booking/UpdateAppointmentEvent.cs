namespace EventAndCommands.Events.Booking;

public class UpdateAppointmentEvent : INotification
{
    public AppointmentEntity? AppointmentEntity { get; set; }
}