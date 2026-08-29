namespace AgendaBuddy.Calendar.Domain.Queries;

[ExcludeFromCodeCoverage]
public class CheckCalendarAvailabilityQuery : IRequest<Result<List<DateTime>>>
{
    /// <summary>The PROVIDER whose free slots are wanted — not the caller.</summary>
    public required string Email { get; set; }

    /// <summary>
    /// Window length in days. Clamped by <see cref="Library.Tools.AvailabilityCalculator"/>, so an
    /// out-of-range value is narrowed rather than rejected.
    /// </summary>
    public int Days { get; set; } = 30;

    /// <summary>
    /// The service being booked, used to size each slot. When null — or when it names a service this
    /// provider does not have — the default session length applies, so availability degrades to a
    /// generic grid rather than to nothing.
    /// </summary>
    public string? ServiceName { get; set; }
}
