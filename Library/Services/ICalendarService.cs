namespace Library.Services;

public interface ICalendarService
{
    Task<IEnumerable<AppointmentEntity>> GetAllAppointmentsAsync();
    Task<IEnumerable<AppointmentEntity>> GetCalendarAppointmentsAsync(BsonDocument filter);
    Task<IEnumerable<AppointmentEntity>> CheckCalendarAvailabilityAsync();
    Task<bool> BlockCalendarPeriodAsync(string emailProvider, DateTime starDate, DateTime endDate);
}