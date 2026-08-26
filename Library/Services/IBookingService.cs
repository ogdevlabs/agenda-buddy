namespace Library.Services;

public interface IBookingService
{
    Task BookAppointmentAsync(AppointmentEntity appointmentEntity);
    Task<bool> UpdateAppointmentAsync(string identifier, AppointmentEntity appointmentEntity);
    Task<bool> CancelAppointmentAsync(string identifier);
    Task<AppointmentEntity> SearchAppointmentAsync(string identifier);
}
