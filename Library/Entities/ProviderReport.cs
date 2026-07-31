namespace Library.Entities;

[ExcludeFromCodeCoverage]
public class ProviderReport
{
    public string ProviderEmail { get; set; } = null!;
    public int TotalBookings { get; set; }
    public int CompletedAppointments { get; set; }
    public int CancelledAppointments { get; set; }
    public decimal EstimatedRevenue { get; set; }
    public int UniqueCustomers { get; set; }
    public double RetentionRate { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
