using System.Linq;

namespace Library.Services;

public class ReportingService(IRepository<ProviderEntity> providerRepository) : IReportingService
{
    public async Task<ProviderReport> GetProviderReportAsync(string providerEmail)
    {
        var filter = new BsonDocument("email", providerEmail);
        var provider = await providerRepository.FindOneAsync(filter)
            ?? throw new KeyNotFoundException($"Provider {providerEmail} not found.");

        var appointments = provider.AppointmentEntities;
        var completed = appointments.Where(a => a.AppointmentStatus == AppointmentStatus.Completed).ToList();
        var booked = appointments.Where(a => a.AppointmentStatus == AppointmentStatus.Booked).ToList();
        var customerEmails = appointments.Select(a => a.EmailCustomer).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        // customers who have more than one appointment are considered returning
        var returningCustomers = appointments
            .GroupBy(a => a.EmailCustomer, StringComparer.OrdinalIgnoreCase)
            .Count(g => g.Count() > 1);
        var retentionRate = customerEmails.Count > 0
            ? (double)returningCustomers / customerEmails.Count * 100.0
            : 0.0;

        // revenue = sum of all active service fees weighted by completed appointment count
        var totalServiceFee = provider.ServiceEntities
            .Where(s => s.IsActive && s.Fee.HasValue)
            .Sum(s => s.Fee!.Value);
        var estimatedRevenue = completed.Count * totalServiceFee;

        return new ProviderReport
        {
            ProviderEmail = providerEmail,
            TotalBookings = appointments.Count,
            CompletedAppointments = completed.Count,
            CancelledAppointments = appointments.Count - completed.Count - booked.Count -
                                    appointments.Count(a => a.AppointmentStatus == AppointmentStatus.Requested),
            EstimatedRevenue = estimatedRevenue,
            UniqueCustomers = customerEmails.Count,
            RetentionRate = Math.Round(retentionRate, 2),
            GeneratedAt = DateTime.UtcNow
        };
    }
}
