namespace Library.Services;

public interface IBookingService
{
    Task BookAppointment(AppointmentEntity appointmentEntity);
    Task<bool> UpdateAppointment(string identifier, AppointmentEntity appointmentEntity);
    Task<bool> CancelAppointment(string identifier);
    Task<AppointmentEntity> SearchAppointment(string identifier);
}