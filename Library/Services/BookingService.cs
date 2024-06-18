
namespace Library.Services;

public class BookingService(IRepository<AppointmentEntity> appointmentRepository) : IBookingService
{
    
    public async Task BookAppointment(AppointmentEntity appointmentEntity)
    {
        await appointmentRepository.InsertAsync(appointmentEntity);
    }

    public async Task<bool> UpdateAppointment(string id, AppointmentEntity appointmentEntity)
    {
        return await appointmentRepository.UpdateAsync(id, appointmentEntity);
    }

    public async Task<bool> CancelAppointment(string id)
    {
        return await appointmentRepository.DeleteAsync(id);
    }
}