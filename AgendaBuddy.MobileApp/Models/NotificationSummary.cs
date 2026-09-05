using CommunityToolkit.Mvvm.ComponentModel;
using AgendaBuddy.Library.Entities;

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

    /// <summary>The bold first line. Falls back to the type when a producer wrote no subject.</summary>
    public string Title => string.IsNullOrWhiteSpace(Subject) ? TypeLabel : Subject;

    /// <summary>The grey second line.</summary>
    public string Message => Body;

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
    public string TypeLabel => Type switch
    {
        NotificationType.AppointmentRequested => "Requested",
        NotificationType.AppointmentBooked => "Booked",
        NotificationType.AppointmentUpdated => "Updated",
        NotificationType.AppointmentCancelled => "Cancelled",
        NotificationType.AppointmentCompleted => "Completed",
        NotificationType.MessageReceived => "Message",
        NotificationType.PasswordResetRequested => "Security",
        NotificationType.EmailConfirmationRequested => "Security",
        _ => "Info"
    };

    /// <summary>
    /// Local time, not UTC. <c>CreatedAt</c> arrives as a UTC instant and the reader is on their own clock, so
    /// comparing it against <c>DateTime.Now</c> without converting reports a fresh notification as hours old
    /// for anyone west of UTC and negative for anyone east.
    /// </summary>
    public string TimeAgo => FormatTimeAgo(CreatedAt.ToLocalTime());

    private static string FormatTimeAgo(DateTime localTime)
    {
        var diff = DateTime.Now - localTime;

        // Clock skew between device and server can put a just-written notification marginally in the future.
        if (diff < TimeSpan.Zero) return "now";

        if (diff.TotalMinutes < 1) return "now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
        return $"{(int)diff.TotalDays}d ago";
    }
}
