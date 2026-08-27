namespace AgendaBuddy.Library.Entities;

/// <summary>
/// A provider's own metrics, computed per request and stored nowhere.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b><c>EstimatedRevenue</c> was removed</b> (ADR D-7). The figure was
/// <c>completed.Count × sum(all active service fees)</c> — completed appointments multiplied by the *whole
/// catalogue total* — which is not revenue under any definition. A provider offering three services at 50, 80
/// and 100 with two completed appointments was reported as having earned 460.
/// </para>
/// <para>
/// <b>It could not be fixed by changing the formula</b>, because <c>AppointmentEntity</c> does not record
/// which service the appointment is for: there is no service reference, no fee and no amount on it. The input
/// does not exist in the stored data.
/// </para>
/// <para>
/// So the report says so. <see cref="RevenueAvailable"/> is a <c>bool</c> rather than a nullable number
/// precisely so a client cannot render <c>null</c> as <c>0</c> — a dashboard reading £0 looks like a business
/// fact, and this one would have been a bug. Publishing a number this system knows to be wrong is the exact
/// defect this class exists to avoid: something marked delivered that does not do what its name says.
/// </para>
/// </remarks>
public class ProviderReport
{
    public string ProviderEmail { get; set; } = null!;
    public int TotalBookings { get; set; }
    public int CompletedAppointments { get; set; }
    public int CancelledAppointments { get; set; }
    public int UniqueCustomers { get; set; }
    public double RetentionRate { get; set; }

    /// <summary>
    /// Always <c>false</c> today. It is a field rather than a constant so that whoever adds the
    /// appointment→service reference can flip it without changing the response shape a client binds to.
    /// </summary>
    public bool RevenueAvailable { get; set; }

    /// <summary>Why revenue is absent. Rendered by the client instead of a figure.</summary>
    public string? RevenueUnavailableReason { get; set; }

    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
