using AgendaBuddy.MobileApp.Models;

namespace AgendaBuddy.MobileApp.Infrastructure;

/// <summary>
/// The showcase's composed sentences. Every one is keyed on the provider's first name, never a pronoun — a
/// pronoun is a guess about somebody's gender that the app has no business making.
/// </summary>
public static class ShowcaseText
{
    public const int PortfolioCapacity = 20;
    public const int TaglineMaxLength = 80;
    public const int AboutMaxLength = 600;
    public const int CaptionMaxLength = 140;

    /// <summary>
    /// The funnel as one plain sentence in the provider's words, not three bare numbers.
    /// </summary>
    /// <remarks>
    /// Each count picks its own form — zero, one, many — because English and Spanish agree the verb with the
    /// count ("1 persona escaneó" / "41 personas escanearon"), so concatenating a number onto one phrase is wrong
    /// in both languages at 1. Nobody having scanned yet gets its own sentence, which says what to do next.
    /// </remarks>
    public static string FunnelSentence(ShowcaseFunnel? funnel)
    {
        if (funnel is null || funnel.Scans <= 0 && funnel.Opened <= 0 && funnel.Booked <= 0)
            return AppResources.GetString("Showcase_Funnel_Zero");

        var window = funnel.WindowDays == 7 || funnel.WindowDays <= 0
            ? AppResources.GetString("Showcase_Funnel_ThisWeek")
            : AppResources.Format("Showcase_Funnel_LastDays", funnel.WindowDays);

        return AppResources.Format(
            "Showcase_Funnel_Sentence",
            window,
            Counted("Showcase_Funnel_Scans", funnel.Scans),
            Counted("Showcase_Funnel_Opened", funnel.Opened),
            Counted("Showcase_Funnel_Booked", funnel.Booked));
    }

    /// <summary>"3 of 5 done".</summary>
    public static string Completeness(ShowcaseCompleteness? completeness) =>
        AppResources.Format("Showcase_CompletenessLabel", completeness?.Done ?? 0, completeness?.Total ?? 5);

    public static double CompletenessProgress(ShowcaseCompleteness? completeness) =>
        completeness is null || completeness.Total <= 0
            ? 0
            : Math.Clamp((double)completeness.Done / completeness.Total, 0, 1);

    /// <summary>"18 of 20".</summary>
    public static string PortfolioCount(int count) =>
        AppResources.Format("Showcase_PortfolioCount", count, PortfolioCapacity);

    public static int RemainingSlots(int count) => Math.Max(0, PortfolioCapacity - count);

    /// <summary>"See Mariana's work".</summary>
    public static string SeeWork(string? firstName) =>
        string.IsNullOrWhiteSpace(firstName)
            ? AppResources.GetString("Showcase_SeeWorkNoName")
            : AppResources.Format("Showcase_SeeWork", firstName.Trim());

    /// <summary>"Opened from Mariana's code".</summary>
    public static string OpenedFromCode(string? firstName) =>
        string.IsNullOrWhiteSpace(firstName)
            ? AppResources.GetString("Showcase_OpenedFromCodeNoName")
            : AppResources.Format("Showcase_OpenedFromCode", firstName.Trim());

    /// <summary>"Mariana is hidden".</summary>
    public static string Hidden(string? firstName) =>
        string.IsNullOrWhiteSpace(firstName)
            ? AppResources.GetString("Showcase_HiddenNoName")
            : AppResources.Format("Showcase_Hidden", firstName.Trim());

    /// <summary>"Photo 3 of 12, Botanical sleeve" — what a screen reader says for a portfolio tile.</summary>
    public static string TileDescription(int position, int count, string? caption) =>
        string.IsNullOrWhiteSpace(caption)
            ? AppResources.Format("Showcase_TileDescription", position, count)
            : AppResources.Format("Showcase_TileDescriptionWithCaption", position, count, caption.Trim());

    /// <summary>"Your next session: Mon 12 Oct, 4:00 PM · Fine-line piece", in the reader's own zone.</summary>
    public static string NextSession(ShowcaseNextAppointment appointment)
    {
        var local = appointment.ScheduledAt.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(appointment.ScheduledAt, DateTimeKind.Utc).ToLocalTime()
            : appointment.ScheduledAt.ToLocalTime();
        var when = local.ToString("ddd d MMM, t", AppResources.CurrentCulture);

        return string.IsNullOrWhiteSpace(appointment.ServiceName)
            ? AppResources.Format("Showcase_NextSession", when)
            : AppResources.Format("Showcase_NextSessionWithService", when, appointment.ServiceName.Trim());
    }

    /// <summary>The first word of a full name, for surfaces that have only the full name.</summary>
    public static string FirstNameOf(string? fullName) =>
        string.IsNullOrWhiteSpace(fullName)
            ? string.Empty
            : fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];

    /// <summary>Length in text elements, the unit the server counts, so an emoji is one character, not two.</summary>
    public static int VisibleLength(string? text) =>
        string.IsNullOrEmpty(text) ? 0 : new System.Globalization.StringInfo(text.Trim()).LengthInTextElements;

    private static string Counted(string keyPrefix, int count) => count switch
    {
        <= 0 => AppResources.GetString(keyPrefix + "_Zero"),
        1 => AppResources.GetString(keyPrefix + "_One"),
        _ => AppResources.Format(keyPrefix + "_Many", count)
    };
}
