namespace AgendaBuddy.Provider.Domain.Commands;

/// <summary>
/// Sets a provider's per-weekday working hours — the windows <c>AvailabilityCalculator</c> generates bookable
/// slots in, resolved per date rather than once for the whole window.
/// </summary>
/// <remarks>
/// Generalises <see cref="SetProviderWorkHoursCommand"/>, which stays: the single pair remains the fallback for
/// any weekday this command does not mention, so every provider stored before per-weekday hours existed keeps
/// exactly the hours they had.
/// </remarks>
[ExcludeFromCodeCoverage]
public class SetProviderWorkWeekCommand : IRequest<Result<ProviderEntity>>
{
    public required string Email { get; set; }

    /// <summary>
    /// One entry per weekday being set. Weekdays absent from this list keep whatever they had.
    /// </summary>
    public required List<WorkDayHours> Days { get; set; }
}
