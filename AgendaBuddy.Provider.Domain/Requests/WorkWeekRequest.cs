using System.ComponentModel.DataAnnotations;

namespace AgendaBuddy.Provider.Domain.Requests;

/// <summary>
/// One weekday's working window, or that weekday marked closed.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="EndHour"/> is exclusive on the same convention as <see cref="WorkHoursRequest"/>: 8–17 means the
/// last session finishes at 17:00. Whole hours only, because the slot grid steps by the hour and a half-past
/// start has nowhere to land.
/// </para>
/// <para>
/// <b>A day is closed by the flag, not by omitting its hours.</b> The two are different statements: closed means
/// "I do not work Sundays", while a day absent from the request keeps whatever it had. Sending hours alongside
/// <see cref="IsClosed"/> is allowed and they are kept, so re-opening a day does not mean re-entering them.
/// </para>
/// </remarks>
[ExcludeFromCodeCoverage]
public class WorkDayRequest : IValidatableObject
{
    /// <summary>
    /// The weekday, as its <see cref="System.DayOfWeek"/> name — <c>"Monday"</c>, <c>"Tuesday"</c>, …
    /// </summary>
    /// <remarks>
    /// A string rather than the enum so an unrecognised value answers <b>400</b> with a usable message, instead
    /// of being model-bound to <c>Sunday</c> (the enum's zero value) and silently rewriting the wrong day.
    /// </remarks>
    [Required(ErrorMessage = "day is required.")]
    public string Day { get; set; } = string.Empty;

    [Range(0, 23, ErrorMessage = "startHour must be between 0 and 23.")]
    public int? StartHour { get; set; }

    [Range(1, 24, ErrorMessage = "endHour must be between 1 and 24.")]
    public int? EndHour { get; set; }

    public bool IsClosed { get; set; }

    /// <summary>The parsed weekday, or <c>null</c> when <see cref="Day"/> names none.</summary>
    public DayOfWeek? ParsedDay =>
        Enum.TryParse<DayOfWeek>(Day, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : null;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ParsedDay is null)
        {
            yield return new ValidationResult(
                $"'{Day}' is not a day of the week. Use Monday, Tuesday, Wednesday, Thursday, Friday, "
                + "Saturday or Sunday.",
                [nameof(Day)]);
        }

        // A closed day needs no window, so neither half of this applies to it.
        if (IsClosed) yield break;

        if (StartHour is null || EndHour is null)
        {
            yield return new ValidationResult(
                $"{Day}: an open day needs both startHour and endHour, or isClosed: true.",
                [nameof(StartHour), nameof(EndHour)]);
            yield break;
        }

        // Rejected, never clamped — the same rule as the single-pair route. Silently correcting the window would
        // leave the provider looking at hours they did not choose; silently accepting it would make them
        // unbookable that day with nothing to explain why.
        if (StartHour >= EndHour)
        {
            yield return new ValidationResult(
                $"{Day}: startHour must be earlier than endHour.",
                [nameof(StartHour), nameof(EndHour)]);
        }
    }
}

/// <summary>
/// A provider's working week: one entry per weekday they are setting.
/// </summary>
/// <remarks>
/// <para>
/// <b>Weekdays absent from the list are left alone</b>, and they inherit the single legacy
/// <c>WorkDayStartHour</c>/<c>WorkDayEndHour</c> pair — which is what keeps every provider stored before this
/// existed bookable on exactly the hours they had. Sending a partial week is therefore a coherent request, not a
/// half-finished one.
/// </para>
/// <para>
/// A duplicate weekday is rejected rather than resolved: two windows for one day is a client bug, and picking one
/// silently would store hours nobody chose.
/// </para>
/// </remarks>
[ExcludeFromCodeCoverage]
public class WorkWeekRequest : IValidatableObject
{
    [Required(ErrorMessage = "days is required.")]
    [MinLength(1, ErrorMessage = "days must contain at least one weekday.")]
    public List<WorkDayRequest> Days { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var duplicates = Days
            .Select(day => day.ParsedDay)
            .Where(day => day is not null)
            .GroupBy(day => day!.Value)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key.ToString())
            .ToList();

        if (duplicates.Count > 0)
        {
            yield return new ValidationResult(
                $"days lists {string.Join(" and ", duplicates)} more than once. Send one window per weekday.",
                [nameof(Days)]);
        }
    }
}
