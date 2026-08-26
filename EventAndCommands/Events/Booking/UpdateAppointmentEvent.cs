namespace EventAndCommands.Events.Booking;

[ExcludeFromCodeCoverage]
public class UpdateAppointmentEvent : INotification
{
    public AppointmentEntity? AppointmentEntity { get; set; }
}
