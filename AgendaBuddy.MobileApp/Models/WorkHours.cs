namespace AgendaBuddy.MobileApp.Models;

/// <summary>
/// A provider's working-day bounds, in whole hours on their own clock. <paramref name="EndHour"/> is
/// exclusive: 8–17 means the last session finishes at 17:00.
/// </summary>
public readonly record struct WorkHours(int StartHour, int EndHour)
{
    /// <summary>What a provider who has never set their hours is working to — mirrors AvailabilityCalculator.</summary>
    public static WorkHours Default => new(8, 17);
}
