using CommunityToolkit.Mvvm.ComponentModel;
using AgendaBuddy.Library.Entities;
using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Resources.Strings;

namespace AgendaBuddy.MobileApp.Models;

/// <summary>
/// One row of the notification inbox, as <c>GET /api/v1/notifications</c> returns it.
/// </summary>
/// <remarks>
/// ⚠️ <b>The property names here are the wire contract.</b> The route serialises
/// <c>NotificationEntity</c> with the default web naming policy, so the JSON is
/// <c>subject</c>/<c>body</c>/<c>type</c>/<c>appointmentIdentifier</c>. This class previously declared
/// <c>Message</c> and <c>NotificationType</c>, which match nothing the backend emits: the body never bound and
/// the type silently defaulted to <c>0</c> (<see cref="NotificationType.AppointmentBooked"/>), so every
/// notification rendered as a blank card reading "Booked" — including cancellations. Nothing failed, because
/// the test for this fed hand-written JSON in the shape the client wanted rather than the shape the route
/// returns. <b>Any change here has to be checked against <c>NotificationEntity</c>, not against a fixture.</b>
/// </remarks>
public partial class NotificationSummary : ObservableObject
{
    public string Id { get; set; } = string.Empty;

    /// <summary>The headline the producer wrote, e.g. "New appointment request".</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>The detail line, e.g. "someone@example.com requested Deep Tissue on Friday 5 September at 2:00 PM".</summary>
    public string Body { get; set; } = string.Empty;

    public NotificationType Type { get; set; }

    /// <summary>
    /// The appointment this notification is about, empty for the ones that are not about an appointment
    /// (a new message, a password reset). What makes the row openable rather than a dead end.
    /// </summary>
    public string AppointmentIdentifier { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    [ObservableProperty]
    private bool _isRead;

    [ObservableProperty]
    private bool _isExpanded;

    /// <summary>
    /// The date band this row is filed under ("Today", "Yesterday", a weekday, a date), set on the first row
    /// of each band and left empty on the rest.
    /// </summary>
    /// <remarks>
    /// Assigned by the view model rather than computed here, because whether a row *starts* a band depends on
    /// the row before it — which the row itself cannot see. A flat list with a header on the boundary rows,
    /// not a grouped <c>CollectionView</c>: same reason the professions catalog is built that way.
    /// </remarks>
    [ObservableProperty]
    private string _sectionHeader = string.Empty;

    /// <summary>Whether this row opens a date band, and so draws the header above its card.</summary>
    public bool StartsSection => !string.IsNullOrEmpty(SectionHeader);

    /// <summary>The bold first line. Known types use privacy-safe local copy; unknown legacy rows stay readable.</summary>
    public string Title => IsKnownType
        ? AppResources.GetString($"Notification_Title_{Type}")
        : string.IsNullOrWhiteSpace(Subject) ? TypeLabel : Subject;

    /// <summary>
    /// The inverse of <see cref="IsRead"/>, as a property the view can bind.
    /// </summary>
    /// <remarks>
    /// Unread is the state the list has to make obvious, so it is what the row's own chrome is driven from —
    /// binding <c>IsRead</c> through an inverting converter at four separate places in the template is the
    /// same information said four times, and one of them will eventually be forgotten.
    /// </remarks>
    public bool IsUnread => !IsRead;

    /// <summary>The strong accent for this notification's kind. See <see cref="NotificationVisuals"/>.</summary>
    public string AccentHex => NotificationVisuals.Accent(Type);

    /// <summary>The soft fill the <see cref="Glyph"/> sits on.</summary>
    public string TintHex => NotificationVisuals.Tint(Type);

    /// <summary>The glyph for this notification's kind, beside (never instead of) <see cref="TypeLabel"/>.</summary>
    public string Glyph => NotificationVisuals.Glyph(Type);

    /// <summary>The grey second line. A user's message is content, not interface copy, and is never translated.</summary>
    public string Message => Type switch
    {
        NotificationType.MessageReceived => Body,
        _ when IsKnownType => AppResources.GetString($"Notification_Body_{Type}"),
        _ => Body
    };

    /// <summary>Whether this notification names an appointment at all.</summary>
    public bool HasAppointment => !string.IsNullOrWhiteSpace(AppointmentIdentifier);

    /// <summary>
    /// Whether the appointment can still be opened.
    /// </summary>
    /// <remarks>
    /// Every notification that names an appointment can now open it, cancellations included: cancelling is a
    /// soft delete, so the appointment survives with <c>Cancelled</c> status and the detail page can fetch it.
    /// This was briefly narrower than <see cref="HasAppointment"/> — cancellation used to hard-delete the
    /// document, so the button led to a page that could fetch nothing. Kept as a distinct property because
    /// "names an appointment" and "can be opened" are different questions, and the next state that cannot be
    /// opened belongs here rather than in the view.
    /// </remarks>
    public bool CanOpenAppointment => HasAppointment;

    /// <summary>
    /// A short category word. Every <see cref="NotificationType"/> is named: falling through to "Info" for
    /// the two types the live producers emit most (<see cref="NotificationType.AppointmentRequested"/> and
    /// <see cref="NotificationType.MessageReceived"/>) labelled a booking request "Info".
    /// </summary>
    public string TypeLabel => IsKnownType
        ? AppResources.GetString($"Notification_Label_{Type}")
        : AppResources.GetString("Notification_Label_Unknown");

    /// <summary>
    /// Local time, not UTC. <c>CreatedAt</c> arrives as a UTC instant and the reader is on their own clock, so
    /// comparing it against <c>DateTime.Now</c> without converting reports a fresh notification as hours old
    /// for anyone west of UTC and negative for anyone east.
    /// </summary>
    public string TimeAgo => FormatTimeAgo(LocalCreatedAt, DateTime.Now);

    /// <summary>
    /// <see cref="CreatedAt"/> on the reader's own clock, for anything that renders an actual time.
    /// </summary>
    /// <remarks>
    /// The expanded row used to format <see cref="CreatedAt"/> directly, so it printed the UTC instant the
    /// server stored — hours off for every reader not on UTC, and disagreeing with the <see cref="TimeAgo"/>
    /// line directly above it, which did convert.
    /// </remarks>
    public DateTime LocalCreatedAt => CreatedAt.ToLocalTime();

    partial void OnIsReadChanged(bool value) => OnPropertyChanged(nameof(IsUnread));

    partial void OnSectionHeaderChanged(string value) => OnPropertyChanged(nameof(StartsSection));

    internal static string FormatTimeAgo(DateTime localTime, DateTime localNow)
    {
        var diff = localNow - localTime;

        // Clock skew between device and server can put a just-written notification marginally in the future.
        if (diff < TimeSpan.Zero) return AppResources.GetString("Notification_Time_Now");

        if (diff.TotalMinutes < 1) return AppResources.GetString("Notification_Time_Now");
        if (diff.TotalMinutes < 60) return AppResources.Format("Notification_Time_MinutesAgo", (int)diff.TotalMinutes);
        if (diff.TotalHours < 24) return AppResources.Format("Notification_Time_HoursAgo", (int)diff.TotalHours);
        return AppResources.Format("Notification_Time_DaysAgo", (int)diff.TotalDays);
    }

    private bool IsKnownType => Enum.IsDefined(Type);
}
