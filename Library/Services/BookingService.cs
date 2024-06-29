namespace Library.Services;

public class BookingService(IRepository<AppointmentEntity> appointmentRepository) : IBookingService
{
    public async Task BookAppointmentAsync(AppointmentEntity appointmentEntity)
    {
        await appointmentRepository.InsertAsync(appointmentEntity);
    }

    public async Task<bool> UpdateAppointmentAsync(string identifier, AppointmentEntity appointmentEntity)
    {
        return await appointmentRepository.UpdateByIdentifierAsync(identifier, appointmentEntity);
    }

    public async Task<bool> CancelAppointmentAsync(string identifier)
    {
        return await appointmentRepository.DeleteByIdentifierAsync(identifier);
    }

    public async Task<AppointmentEntity> SearchAppointmentAsync(string identifier)
    {
        var filter = new BsonDocument("identifier", identifier);
        return await appointmentRepository.Find(filter);
    }
}