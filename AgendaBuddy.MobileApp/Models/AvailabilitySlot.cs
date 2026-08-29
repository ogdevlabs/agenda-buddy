namespace AgendaBuddy.MobileApp.Models;

/// <summary>
/// One bookable start time: the exact instant to send back, plus how it reads on this device.
/// </summary>
/// <remarks>
/// Named for what it is rather than <c>TimeSlot</c>, which <c>CalendarDaySummary.cs</c> already defines as
/// an unrelated (and currently unused) display type. Two same-named models in one namespace would not
/// compile, and picking the more specific name here leaves that one untouched.
/// </remarks>
/// <remarks>
/// Both halves are needed and they are not interchangeable. <see cref="StartUtc"/> is the instant the
/// server offered and must be POSTed back unchanged, or the booking lands at a different time than the one
/// that was shown. <see cref="Label"/> is that instant in the DEVICE'S timezone, which is the only reading
/// that means anything to whoever is looking at the screen — binding the raw UTC value straight into a
/// <c>{0:h:mm tt}</c> format showed a 17:00Z slot as "5:00 PM" to a user at UTC-6, for whom it is 11:00 AM.
/// </remarks>
public sealed record AvailabilitySlot(DateTime StartUtc)
{
    /// <summary>The same instant on this device's clock.</summary>
    public DateTime LocalStart { get; } = StartUtc.ToLocalTime();

    /// <summary>Wall-clock time as this device reads it.</summary>
    public string Label => LocalStart.ToString("h:mm tt");
}
