namespace AgendaBuddy.Provider.Domain.Commands;

/// <summary>
/// Sets the provider's working-day bounds — the window <c>AvailabilityCalculator</c> generates bookable
/// slots in. Hours are whole hours on the provider's own clock, and <see cref="EndHour"/> is exclusive.
/// </summary>
[ExcludeFromCodeCoverage]
public class SetProviderWorkHoursCommand : IRequest<Result<ProviderEntity>>
{
    public required string Email { get; set; }

    public required int StartHour { get; set; }

    public required int EndHour { get; set; }
}
