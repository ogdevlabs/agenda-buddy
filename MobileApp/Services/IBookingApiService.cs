using MobileApp.Models;

namespace MobileApp.Services;

public interface IBookingApiService
{
    Task<List<AppointmentSummary>> GetTodayAppointmentsAsync(CancellationToken ct = default);
    Task<AppointmentSummary?> UpdateStatusAsync(string id, string status, CancellationToken ct = default);
}
