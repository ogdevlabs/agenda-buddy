namespace Library.Services;

public interface IBookingService
{
    Task BookAppointment(AppointmentEntity appointmentEntity);
    Task<bool> UpdateAppointment(string id, AppointmentEntity appointmentEntity);
    Task<bool> CancelAppointment(string id);
}