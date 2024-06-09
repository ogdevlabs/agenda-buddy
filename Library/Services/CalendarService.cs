namespace Library.Services;

public class CalendarService(IRepository<AppointmentEntity> appointmentRepository) : ICalendarService
{
    public async Task<IEnumerable<AppointmentEntity>> GetCalendarAppointments()
    {
        return await appointmentRepository.GetAllAsync();
    }

    public async Task<IEnumerable<AppointmentEntity>> CheckCalendarAvailability()
    {
        return await appointmentRepository.GetAllAsync();
    }

    public async Task<bool> BlockCalendarPeriod(DateTime startDate, DateTime endDate)
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
                    IsBooked = false,
                    Appointment = DateTime.Today,
                    DayOff = true
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