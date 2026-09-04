using System.ComponentModel.DataAnnotations;

namespace AgendaBuddy.Provider.Domain.Requests;

/// <summary>
/// The provider's working-day bounds, in whole hours on their own clock. <see cref="EndHour"/> is
/// exclusive, so 8–17 means the last session finishes at 17:00.
/// </summary>
/// <remarks>
/// A window that opens at or after it closes is rejected rather than clamped: silently correcting it would
/// leave the provider looking at hours they did not choose, and silently accepting it would make them
/// unbookable with nothing to explain why.
/// </remarks>
[ExcludeFromCodeCoverage]
public class WorkHoursRequest : IValidatableObject
{
    [Range(0, 23, ErrorMessage = "startHour must be between 0 and 23.")]
    public int StartHour { get; set; }

    [Range(1, 24, ErrorMessage = "endHour must be between 1 and 24.")]
    public int EndHour { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartHour >= EndHour)
        {
            yield return new ValidationResult(
                "startHour must be earlier than endHour.",
                [nameof(StartHour), nameof(EndHour)]);
        }
    }
}
