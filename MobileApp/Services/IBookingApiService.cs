using Library.Entities;
using MobileApp.Models;

namespace MobileApp.Services;

public interface IBookingApiService
{
    Task<List<AppointmentSummary>> GetTodayAppointmentsAsync(CancellationToken ct = default);
    Task<AppointmentDetail?> GetAppointmentAsync(string id, CancellationToken ct = default);
    Task<AppointmentDetail?> UpdateStatusAsync(string id, AppointmentStatus status, CancellationToken ct = default);
}
