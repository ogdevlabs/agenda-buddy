namespace Library.Services;

public interface ICalendarService
{
    Task<IEnumerable<AppointmentEntity>> GetCalendarAppointments();
    Task<IEnumerable<AppointmentEntity>> CheckCalendarAvailability();
    Task<bool> BlockCalendarPeriod(DateTime starDate, DateTime endDate);
}