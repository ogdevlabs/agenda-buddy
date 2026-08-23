using System.Linq;

namespace Library.Services;

public class ReportingService(IRepository<ProviderEntity> providerRepository) : IReportingService
{
    /// <summary>
    /// Why the report carries no revenue figure. Rendered by the client in place of a number.
    /// </summary>
    public const string RevenueUnavailable =
        "Appointments do not record which service they were booked for, so revenue cannot be computed from "
        + "stored data.";

    /// <summary>
    /// Counts a provider's appointments by status.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠️ <b>Every number here was structurally zero until F-014.</b> The counts come from
    /// <see cref="AppointmentStatus"/>, and nothing in production ever set anything other than
    /// <c>Requested</c>: <c>Book()</c> and <c>Complete()</c> were never called, and the update path copied
    /// whatever status a client happened to send. Wiring this service without fixing that would have shipped a
    /// dashboard reporting 0 completed appointments and 0 revenue forever — worse than leaving it unwired,
    /// because an unreachable endpoint is obviously broken while a dashboard reading zero looks like a fact.
    /// F-014 made status server-owned (threat T-203), which is what makes these counts mean anything.
    /// </para>
    /// <para>
    /// <b>Revenue is deliberately not computed.</b> See <see cref="ProviderReport"/> for the full reasoning:
    /// the old formula multiplied completed appointments by the entire service catalogue's fees, and the data
    /// needed to do it correctly — which service an appointment was for — is not stored anywhere.
    /// </para>
    /// <para>
    /// Counts read the <b>embedded</b> appointment list on the provider document, not the `appointments`
    /// collection. That is why F-014's status change writes both copies: updating only the collection would
    /// leave this report showing the old status indefinitely.
    /// </para>
    /// </remarks>
    public async Task<ProviderReport> GetProviderReportAsync(string providerEmail)
    {
        var filter = new BsonDocument("email", providerEmail);
        var provider = await providerRepository.FindOneAsync(filter)
            ?? throw new KeyNotFoundException($"Provider {providerEmail} not found.");

        var appointments = provider.AppointmentEntities;
        var completed = appointments.Count(a => a.AppointmentStatus == AppointmentStatus.Completed);
        var booked = appointments.Count(a => a.AppointmentStatus == AppointmentStatus.Booked);
        var requested = appointments.Count(a => a.AppointmentStatus == AppointmentStatus.Requested);
        var customerEmails = appointments
            .Select(a => a.EmailCustomer)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // customers who have more than one appointment are considered returning
        var returningCustomers = appointments
            .GroupBy(a => a.EmailCustomer, StringComparer.OrdinalIgnoreCase)
            .Count(g => g.Count() > 1);
        var retentionRate = customerEmails.Count > 0
            ? (double)returningCustomers / customerEmails.Count * 100.0
            : 0.0;

        return new ProviderReport
        {
            ProviderEmail = providerEmail,
            TotalBookings = appointments.Count,
            CompletedAppointments = completed,

            // Whatever is left once the three live states are accounted for. Cancellation currently
            // hard-deletes rather than setting AppointmentStatus.Cancelled, so in practice this is 0 — it is
            // computed rather than hardcoded so it starts working if F-024 ever makes cancellation a soft
            // delete.
            CancelledAppointments = appointments.Count - completed - booked - requested,

            UniqueCustomers = customerEmails.Count,
            RetentionRate = Math.Round(retentionRate, 2),
            RevenueAvailable = false,
            RevenueUnavailableReason = RevenueUnavailable,
            GeneratedAt = DateTime.UtcNow
        };
    }
}
