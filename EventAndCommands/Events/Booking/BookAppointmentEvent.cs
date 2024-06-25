namespace EventAndCommands.Events.Booking;

public class BookAppointmentEvent : INotification
{ 
    public AppointmentEntity? AppointmentEntity { get; set; }
}