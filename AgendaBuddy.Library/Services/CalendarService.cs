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

    public async Task<bool> BlockCalendarPeriodAsync(string emailProvider, DateTime startDate, DateTime endDate)
    {
        var numberOfDays = (int)(endDate - startDate).TotalDays;

        for (var i = 0; i < numberOfDays; i++)
        {
            var blockDay = new AppointmentEntity
            {
                Identifier = Guid.NewGuid().ToString(),
                AppointmentStatus = AppointmentStatus.Confirmed,
                Start = startDate.AddDays(i),
                End = startDate.AddDays(i + 1),
                DayOff = true,
                EmailProvider = emailProvider,
                EmailCustomer = string.Empty
            };
            await appointmentRepository.InsertAsync(blockDay);
        }

        return true;
    }
}
