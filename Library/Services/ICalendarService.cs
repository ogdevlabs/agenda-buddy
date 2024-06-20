namespace Library.Services;

public interface ICalendarService
{
    Task<IEnumerable<AppointmentEntity>> GetAllAppointments();
    Task<IEnumerable<AppointmentEntity>> GetCalendarAppointments(BsonDocument filter);
    Task<IEnumerable<AppointmentEntity>> CheckCalendarAvailability();
    Task<bool> BlockCalendarPeriod(string emailProvider, DateTime starDate, DateTime endDate);
}