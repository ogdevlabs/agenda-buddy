namespace AgendaBuddy.MobileApp.Infrastructure;

/// <summary>
/// The Shell navigation payload for opening an appointment.
/// </summary>
/// <remarks>
/// <para>
/// Shared because two screens now open the same page — the dashboard and the calendar — and
/// <c>AppointmentDetailPage</c> reads <b>eleven</b> query properties. Two independently-built dictionaries is two
/// chances for one to omit a key, and the one that matters is <c>scheduledAt</c>: it is the tell the page uses to
/// decide whether the caller had the appointment in hand. Without it the page treats the navigation as
/// identifier-only and falls back to a fetch, which is correct but slower and shows an error state if it misses.
/// </para>
/// <para>
/// Deliberately free of MAUI types so it is covered on the <c>net10.0</c> test slice, where <c>Shell</c> does not
/// exist.
/// </para>
/// </remarks>
public static class AppointmentNavigation
{
    /// <summary>The Shell route registered for the appointment detail page.</summary>
    public const string Route = "appointmentDetail";

    /// <summary>
    /// Everything <c>AppointmentDetailPage</c> reads, so it can render without waiting on a fetch.
    /// </summary>
    /// <remarks>
    /// Two overloads, because the two callers hold different types: the dashboard lists
    /// <see cref="Models.AppointmentSummary"/> and the calendar holds
    /// <see cref="Models.AppointmentDetail"/>. Both forward to one builder, so the KEYS live in a single place —
    /// which is the whole point, since a missing key degrades silently rather than failing.
    /// </remarks>
    public static Dictionary<string, object> BuildQuery(Models.AppointmentDetail appointment) =>
        Build(
            appointment.Id, appointment.CustomerEmail, appointment.CustomerName, appointment.CustomerPhone,
            appointment.ProviderName, appointment.DisplayName, appointment.ScheduledAt,
            appointment.Status.ToString(), appointment.ServiceName, appointment.ServiceDurationMinutes,
            appointment.CustomerNotes, appointment.ContactEmail, appointment.ContactAvatarId);

    /// <inheritdoc cref="BuildQuery(Models.AppointmentDetail)"/>
    public static Dictionary<string, object> BuildQuery(Models.AppointmentSummary appointment) =>
        Build(
            appointment.Id, appointment.CustomerEmail, appointment.CustomerName, appointment.CustomerPhone,
            appointment.ProviderName, appointment.DisplayName, appointment.ScheduledAt,
            appointment.Status.ToString(), appointment.ServiceName, appointment.ServiceDurationMinutes,
            appointment.CustomerNotes, appointment.ContactEmail, appointment.ContactAvatarId);

    private static Dictionary<string, object> Build(
        string id,
        string customerEmail,
        string customerName,
        string customerPhone,
        string providerName,
        string displayName,
        DateTime scheduledAt,
        string status,
        string serviceName,
        int? serviceDurationMinutes,
        string? customerNotes,
        string? contactEmail,
        string? contactAvatarId) =>
        new()
        {
            ["appointmentId"] = id,
            ["customerEmail"] = customerEmail,
            ["customerName"] = customerName,
            ["customerPhone"] = customerPhone,
            ["providerName"] = providerName,
            ["displayName"] = displayName,

            // Round-trip format, and the key the page keys its fallback decision on.
            ["scheduledAt"] = scheduledAt.ToString("O"),
            ["status"] = status,
            ["serviceName"] = serviceName,
            ["serviceDurationMinutes"] = serviceDurationMinutes?.ToString() ?? string.Empty,
            ["customerNotes"] = customerNotes ?? string.Empty,

            // Carried so the fallback render shows the SAME mark the list the caller came from showed. Without
            // it the detail page would derive one from the address and disagree with the row that opened it.
            ["contactEmail"] = contactEmail ?? string.Empty,
            ["contactAvatarId"] = contactAvatarId ?? string.Empty
        };
}
