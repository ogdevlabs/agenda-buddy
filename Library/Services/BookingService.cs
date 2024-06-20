
namespace Library.Services;

public class BookingService(IRepository<AppointmentEntity> appointmentRepository) : IBookingService
{
    
    public async Task BookAppointment(AppointmentEntity appointmentEntity)
    {
        await appointmentRepository.InsertAsync(appointmentEntity);
    }

    public async Task<bool> UpdateAppointment(string identifier, AppointmentEntity appointmentEntity)
    {
        return await appointmentRepository.UpdateAsync(identifier, appointmentEntity);
    }

    public async Task<bool> CancelAppointment(string identifier)
    {
        return await appointmentRepository.DeleteAsync(identifier);
    }
}