namespace AgendaBuddy.Library.Services;

public interface ICalendarService
{
    Task<IEnumerable<AppointmentEntity>> GetAllAppointmentsAsync();
    Task<IEnumerable<AppointmentEntity>> GetCalendarAppointmentsAsync(BsonDocument filter);
    Task<IEnumerable<AppointmentEntity>> CheckCalendarAvailabilityAsync();

    // BlockCalendarPeriodAsync was removed: it wrote whole-day fake appointments carrying an empty customer
    // email and AppointmentStatus.Confirmed (a status that is never persisted), no route or handler ever called
    // it, and its day loop wrote nothing at all when start and end fell on the same date. Time off is
    // ICalendarBlockService's, on its own collection.
}
