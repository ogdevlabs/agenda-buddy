namespace AgendaBuddy.Library.Calendar;

/// <summary>
/// Turns one person's appointments into the events their subscribed calendar shows.
/// </summary>
/// <remarks>
/// <para>
/// <b>Content is deliberately minimal.</b> A feed URL is a bearer capability that can be forwarded or leaked, so an
/// event carries the service, the other party's display name, the time and a status — never an email address, phone
/// number, fee, payment record, note or time-off reason.
/// </para>
/// <para>
/// A pending reschedule stays at the ORIGINAL time: the appointment has not moved until the other party answers, and
/// every calendar is meant to show where it actually stands. Cancelled appointments are emitted as
/// <c>STATUS:CANCELLED</c> with a title prefix rather than dropped, because some subscribers keep an event that
/// simply disappears and others ignore the status.
/// </para>
/// </remarks>
public static class CalendarFeedProjection
{
    public const string CalendarName = "AgendaMe";
    public const int DaysBack = 60;
    public const int DaysAhead = 365;
    public const int MaxEvents = 500;

    public static IReadOnlyList<IcsEvent> Project(
        IEnumerable<AppointmentEntity> appointments,
        string ownerEmail,
        IReadOnlyDictionary<string, string> displayNamesByEmail,
        string? language,
        DateTime nowUtc)
    {
        var text = FeedText.For(language);
        var from = nowUtc.AddDays(-DaysBack);
        var to = nowUtc.AddDays(DaysAhead);

        return appointments
            .Where(appointment => !appointment.DayOff)
            .Where(appointment => appointment.Start >= from && appointment.Start <= to)
            .OrderBy(appointment => Math.Abs((appointment.Start - nowUtc).Ticks))
            .Take(MaxEvents)
            .OrderBy(appointment => appointment.Start)
            .Select(appointment => ToEvent(appointment, ownerEmail, displayNamesByEmail, text))
            .ToList();
    }

    public static string CounterpartyEmail(AppointmentEntity appointment, string ownerEmail) =>
        string.Equals(appointment.EmailProvider, ownerEmail, StringComparison.OrdinalIgnoreCase)
            ? appointment.EmailCustomer
            : appointment.EmailProvider;

    public static string Uid(AppointmentEntity appointment) => $"{appointment.Identifier}@agendame";

    private static IcsEvent ToEvent(
        AppointmentEntity appointment,
        string ownerEmail,
        IReadOnlyDictionary<string, string> displayNamesByEmail,
        FeedText text)
    {
        var service = string.IsNullOrWhiteSpace(appointment.ServiceName) ? text.Appointment : appointment.ServiceName.Trim();
        displayNamesByEmail.TryGetValue(CounterpartyEmail(appointment, ownerEmail), out var name);
        var title = string.IsNullOrWhiteSpace(name) ? service : string.Format(text.WithName, service, name.Trim());

        var status = appointment.AppointmentStatus switch
        {
            AppointmentStatus.Requested => IcsEventStatus.Tentative,
            AppointmentStatus.Cancelled => IcsEventStatus.Cancelled,
            _ => IcsEventStatus.Confirmed
        };

        var summary = status switch
        {
            IcsEventStatus.Tentative => text.PendingPrefix + title,
            IcsEventStatus.Cancelled => text.CancelledPrefix + title,
            _ => title
        };

        var description = appointment.HasPendingReschedule
            ? text.Description + "\n" + text.RescheduleProposed
            : text.Description;

        return new IcsEvent(Uid(appointment), appointment.Start, appointment.EffectiveEndUtc, summary, description, status);
    }

    private sealed record FeedText(
        string Appointment,
        string WithName,
        string PendingPrefix,
        string CancelledPrefix,
        string Description,
        string RescheduleProposed)
    {
        private static readonly FeedText English = new(
            "Appointment",
            "{0} with {1}",
            "Pending: ",
            "Cancelled: ",
            "Managed in AgendaMe. Open the app to view or change this appointment.",
            "A new time has been proposed. Open AgendaMe to answer.");

        private static readonly FeedText Spanish = new(
            "Cita",
            "{0} con {1}",
            "Pendiente: ",
            "Cancelada: ",
            "Gestionada en AgendaMe. Abre la app para ver o cambiar esta cita.",
            "Se propuso un nuevo horario. Abre AgendaMe para responder.");

        public static FeedText For(string? language) =>
            CalendarFeedLanguage.Normalize(language) == CalendarFeedLanguage.Spanish ? Spanish : English;
    }
}

public static class CalendarFeedLanguage
{
    public const string English = "en";
    public const string Spanish = "es";

    public static string Normalize(string? language) =>
        language is not null && language.Trim().StartsWith("es", StringComparison.OrdinalIgnoreCase) ? Spanish : English;
}
