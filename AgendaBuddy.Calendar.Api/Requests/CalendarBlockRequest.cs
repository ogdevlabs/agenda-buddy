using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace AgendaBuddy.Calendar.Requests;

/// <summary>
/// A period the provider is unavailable, plus their own note about why.
/// </summary>
/// <remarks>
/// <para>
/// A time RANGE, not a date: an afternoon off must leave the morning bookable, which is exactly what the
/// whole-day mechanism this replaces could not express. Multi-day is one long interval. <see cref="EndUtc"/> is
/// exclusive, so 09:00–12:00 leaves 12:00 bookable.
/// </para>
/// <para>
/// ⚠️ <see cref="Reason"/> is the PROVIDER'S OWN note and is never returned on a customer-facing route. The
/// availability response is free start times only — that busy time is inferable as absence is inherent to a
/// booking product, but why a provider is away is not.
/// </para>
/// <para>
/// <b>No provider email:</b> it comes from the path, which is ownership-guarded against the caller's own claim.
/// </para>
/// </remarks>
[ExcludeFromCodeCoverage]
public class CalendarBlockRequest : IValidatableObject
{
    public DateTime StartUtc { get; set; }

    public DateTime EndUtc { get; set; }

    [StringLength(200, ErrorMessage = "reason cannot exceed 200 characters.")]
    public string? Reason { get; set; }

    /// <summary>
    /// Block the range even though live appointments fall inside it.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>false</c> and the refusal names how many are in the way. Blocking over existing bookings
    /// silently would leave the provider believing they were free while customers still held sessions that had
    /// vanished from availability but from nobody's calendar.
    /// </remarks>
    public bool Force { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartUtc == default || EndUtc == default)
        {
            yield return new ValidationResult(
                "startUtc and endUtc are both required.", [nameof(StartUtc), nameof(EndUtc)]);
            yield break;
        }

        // Rejected, never clamped: widening or flipping a provider's time off on their behalf blocks time they
        // never asked to block.
        if (EndUtc <= StartUtc)
        {
            yield return new ValidationResult(
                "endUtc must be after startUtc.", [nameof(StartUtc), nameof(EndUtc)]);
        }
    }
}
