namespace Library.Services;

public interface IBookingService
{
    Task<AppointmentEntity> BookAppointment(AppointmentEntity appointmentEntity);
    Task<AppointmentEntity> UpdateAppointment(AppointmentEntity appointmentEntity);
    Task<AppointmentEntity> CancelAppointment(AppointmentEntity appointmentEntity);
}