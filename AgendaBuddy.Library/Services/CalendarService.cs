namespace AgendaBuddy.Library.Services;

public class CalendarService(IRepository<AppointmentEntity> appointmentRepository) : ICalendarService
{
    public async Task<IEnumerable<AppointmentEntity>> GetAllAppointmentsAsync()
    {
        return await appointmentRepository.GetAllAsync();
    }

    public async Task<IEnumerable<AppointmentEntity>> GetCalendarAppointmentsAsync(BsonDocument filter)
    {
        return await appointmentRepository.FindAllAsync(filter);
    }

    public async Task<IEnumerable<AppointmentEntity>> CheckCalendarAvailabilityAsync()
    {
        var filter = new BsonDocument("day_off", false);
        return await appointmentRepository.FindAllAsync(filter);
    }

}
