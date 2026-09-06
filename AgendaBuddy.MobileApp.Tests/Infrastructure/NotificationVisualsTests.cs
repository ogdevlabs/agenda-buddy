using AgendaBuddy.Library.Entities;
using AgendaBuddy.MobileApp.Infrastructure;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Infrastructure;

/// <summary>
/// The per-type glyph, accent and date banding. Testable at all because none of it mentions a MAUI type — the
/// same reason <c>Routing/</c> exists.
/// </summary>
public class NotificationVisualsTests
{
    public static TheoryData<NotificationType> EveryType()
    {
        var data = new TheoryData<NotificationType>();
        foreach (var type in Enum.GetValues<NotificationType>())
            data.Add(type);
        return data;
    }

    /// <summary>
    /// Every declared type gets its own accent. A member falling through to the neutral default makes a
    /// cancellation look like an informational message — the same defect class as <c>TypeLabel</c> rendering a
    /// booking request as "Info", which is why that has its own member-count assertion server-side.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryType))]
    public void EveryNotificationTypeHasItsOwnAccentAndTint(NotificationType type)
    {
        Assert.NotEqual(NotificationVisuals.NeutralAccent, NotificationVisuals.Accent(type));
        Assert.NotEqual(NotificationVisuals.NeutralTint, NotificationVisuals.Tint(type));
    }

    [Theory]
    [MemberData(nameof(EveryType))]
    public void EveryNotificationTypeHasAGlyph(NotificationType type)
    {
        Assert.False(string.IsNullOrWhiteSpace(NotificationVisuals.Glyph(type)));
    }

    [Theory]
    [MemberData(nameof(EveryType))]
    public void EveryAccentAndTintIsAParseableHex(NotificationType type)
    {
        Assert.Matches("^#[0-9A-Fa-f]{6}$", NotificationVisuals.Accent(type));
        Assert.Matches("^#[0-9A-Fa-f]{6}$", NotificationVisuals.Tint(type));
    }

    /// <summary>An unmapped value is neutral, never alarming — it must not borrow the cancellation red.</summary>
    [Fact]
    public void AnUnknownTypeFallsBackToNeutral()
    {
        var unknown = (NotificationType)9999;

        Assert.Equal(NotificationVisuals.NeutralAccent, NotificationVisuals.Accent(unknown));
        Assert.Equal(NotificationVisuals.NeutralTint, NotificationVisuals.Tint(unknown));
        Assert.False(string.IsNullOrWhiteSpace(NotificationVisuals.Glyph(unknown)));
    }

    /// <summary>The two types that carry the same meaning to a reader share an accent, and that is deliberate.</summary>
    [Fact]
    public void TheTwoSecurityTypesShareOneAccent()
    {
        Assert.Equal(
            NotificationVisuals.Accent(NotificationType.PasswordResetRequested),
            NotificationVisuals.Accent(NotificationType.EmailConfirmationRequested));
    }

    // ── Date banding ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ANotificationFromEarlierTodayIsBandedToday()
    {
        var now = new DateTime(2026, 9, 6, 14, 0, 0, DateTimeKind.Unspecified);

        Assert.Equal("Today", NotificationVisuals.Section(now.AddHours(-3), now));
    }

    /// <summary>
    /// Clock skew between device and server can put a just-written notification marginally in the future.
    /// "In 4 minutes" is not a band, so anything at or after today reads as Today.
    /// </summary>
    [Fact]
    public void ANotificationDatedAheadOfNowIsStillBandedToday()
    {
        var now = new DateTime(2026, 9, 6, 23, 58, 0, DateTimeKind.Unspecified);

        Assert.Equal("Today", NotificationVisuals.Section(now.AddMinutes(10), now));
    }

    [Fact]
    public void YesterdayIsNamedRatherThanDated()
    {
        var now = new DateTime(2026, 9, 6, 9, 0, 0, DateTimeKind.Unspecified);

        Assert.Equal("Yesterday", NotificationVisuals.Section(now.AddDays(-1), now));
    }

    /// <summary>
    /// Just before midnight yesterday and just after midnight today are minutes apart but belong to different
    /// bands: the band is the calendar day, not an elapsed-time bucket.
    /// </summary>
    [Fact]
    public void TheBandFollowsTheCalendarDayNotElapsedTime()
    {
        var now = new DateTime(2026, 9, 6, 0, 10, 0, DateTimeKind.Unspecified);

        Assert.Equal("Yesterday", NotificationVisuals.Section(now.AddMinutes(-20), now));
        Assert.Equal("Today", NotificationVisuals.Section(now, now));
    }

    [Fact]
    public void WithinTheLastWeekTheWeekdayIsNamed()
    {
        var now = new DateTime(2026, 9, 6, 9, 0, 0, DateTimeKind.Unspecified);
        var threeDaysAgo = now.AddDays(-3);

        Assert.Equal(threeDaysAgo.ToString("dddd"), NotificationVisuals.Section(threeDaysAgo, now));
    }

    // A weekday name a week back no longer says when, so it becomes a date.
    [Fact]
    public void BeyondAWeekTheDateIsNamed()
    {
        var now = new DateTime(2026, 9, 6, 9, 0, 0, DateTimeKind.Unspecified);
        var longAgo = now.AddDays(-30);

        Assert.Equal(longAgo.ToString("MMMM d"), NotificationVisuals.Section(longAgo, now));
    }
}
