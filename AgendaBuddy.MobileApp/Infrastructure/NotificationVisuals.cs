using AgendaBuddy.Library.Entities;

namespace AgendaBuddy.MobileApp.Infrastructure;

/// <summary>
/// What tells a reader which kind of notification a row is before they read a word of it: a glyph, an accent,
/// and the date band the row belongs under.
/// </summary>
/// <remarks>
/// <para>
/// Plain strings and static methods, with no MAUI type anywhere, so the whole mapping is covered by the
/// <c>net10.0</c> test slice — the same reason <c>Routing/</c> exists. <see cref="HexColorConverter"/> turns
/// the hex into a <c>Color</c> at the XAML boundary.
/// </para>
/// <para>
/// The hexes are the palette in <c>App.xaml</c>, restated here because a resource key cannot be selected by
/// an enum value in XAML without a converter per key. <see cref="Accent"/> must answer for <b>every</b>
/// <see cref="NotificationType"/>: a new member falling through to the neutral default makes an appointment
/// cancellation look like an informational message, which is the same class of defect as
/// <c>TypeLabel</c> rendering a booking request as "Info".
/// </para>
/// </remarks>
public static class NotificationVisuals
{
    /// <summary>The accent for a type this does not know about. Neutral on purpose — never alarming.</summary>
    public const string NeutralAccent = "#64748B";

    /// <summary>The tint behind <see cref="NeutralAccent"/>.</summary>
    public const string NeutralTint = "#F1F5F9";

    /// <summary>Cutoff between naming a weekday and naming a date. A week back, a weekday name stops helping.</summary>
    private const int WeekdayNamingWindowDays = 7;

    /// <summary>The strong colour: the unread dot, and the type label.</summary>
    public static string Accent(NotificationType type) => type switch
    {
        NotificationType.AppointmentRequested => "#F59E0B",
        NotificationType.AppointmentBooked => "#10B981",
        NotificationType.AppointmentUpdated => "#3B82F6",
        NotificationType.AppointmentCancelled => "#EF4444",
        NotificationType.AppointmentCompleted => "#4F46E5",
        NotificationType.MessageReceived => "#8B5CF6",
        NotificationType.PasswordResetRequested => "#0EA5E9",
        NotificationType.EmailConfirmationRequested => "#0EA5E9",
        _ => NeutralAccent
    };

    /// <summary>The soft fill the glyph sits on. Light enough that the glyph and the title stay readable.</summary>
    public static string Tint(NotificationType type) => type switch
    {
        NotificationType.AppointmentRequested => "#FEF3C7",
        NotificationType.AppointmentBooked => "#D1FAE5",
        NotificationType.AppointmentUpdated => "#DBEAFE",
        NotificationType.AppointmentCancelled => "#FEE2E2",
        NotificationType.AppointmentCompleted => "#EEF2FF",
        NotificationType.MessageReceived => "#EDE9FE",
        NotificationType.PasswordResetRequested => "#E0F2FE",
        NotificationType.EmailConfirmationRequested => "#E0F2FE",
        _ => NeutralTint
    };

    /// <summary>
    /// The glyph. An emoji rather than an icon asset because it needs no per-density export and renders on
    /// both platforms; it is decoration beside the type label, never the only thing carrying the meaning.
    /// </summary>
    public static string Glyph(NotificationType type) => type switch
    {
        NotificationType.AppointmentRequested => "\U0001F5D3",
        NotificationType.AppointmentBooked => "✅",
        NotificationType.AppointmentUpdated => "\U0001F504",
        NotificationType.AppointmentCancelled => "❌",
        NotificationType.AppointmentCompleted => "\U0001F3C1",
        NotificationType.MessageReceived => "\U0001F4AC",
        NotificationType.PasswordResetRequested => "\U0001F510",
        NotificationType.EmailConfirmationRequested => "✉",
        _ => "\U0001F514"
    };

    /// <summary>
    /// The date band a notification belongs under, so a long inbox is scannable by when rather than only by
    /// a per-row "3d ago".
    /// </summary>
    /// <param name="localCreatedAt">The notification's timestamp, already in the reader's own zone.</param>
    /// <param name="localNow">Now, in the same zone. A parameter so the banding is deterministic in a test.</param>
    /// <remarks>
    /// Anything dated ahead of today still reads as "Today": clock skew between device and server can put a
    /// just-written notification marginally in the future, and "in 4 minutes" is not a band.
    /// </remarks>
    public static string Section(DateTime localCreatedAt, DateTime localNow)
    {
        var day = localCreatedAt.Date;
        var today = localNow.Date;

        if (day >= today) return "Today";
        if (day == today.AddDays(-1)) return "Yesterday";
        if (day > today.AddDays(-WeekdayNamingWindowDays)) return localCreatedAt.ToString("dddd");

        return localCreatedAt.ToString("MMMM d");
    }
}
