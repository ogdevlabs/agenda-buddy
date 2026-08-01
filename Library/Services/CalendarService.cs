namespace Library.Services;

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
        // TODO
        return await appointmentRepository.GetAllAsync();
    }

    public async Task<bool> BlockCalendarPeriodAsync(string emailProvider, DateTime startDate, DateTime endDate)
    {
        try
        {
            // Calculate the difference between the two dates
            var difference = endDate - startDate;

            // Get the number of days
            var numberOfDays = difference.TotalDays;
            for (var i = 0; i < numberOfDays; i++)
            {
                var blockDay = new AppointmentEntity
                {
                    Identifier = Guid.NewGuid().ToString(),
                    AppointmentStatus = AppointmentStatus.Requested,
                    Start = DateTime.Today,
                    DayOff = true,
                    EmailProvider = emailProvider,
                    EmailCustomer = string.Empty
                };
                await appointmentRepository.InsertAsync(blockDay);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}