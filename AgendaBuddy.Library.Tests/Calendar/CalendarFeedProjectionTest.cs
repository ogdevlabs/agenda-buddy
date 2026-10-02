using AgendaBuddy.Library.Calendar;
using AgendaBuddy.Library.Entities;
using Xunit;

namespace AgendaBuddy.Library.Tests.Calendar;

public class CalendarFeedProjectionTest
{
    private const string Provider = "coach@example.com";
    private const string Customer = "client@example.com";
    private static readonly DateTime Now = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private static readonly Dictionary<string, string> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        [Provider] = "Ana Coach",
        [Customer] = "Luis Client"
    };

    [Fact]
    public void ProviderSeesServiceWithCustomerName()
    {
        var events = Project([Appointment(AppointmentStatus.Booked)], Provider);

        Assert.Equal("Yoga with Luis Client", Assert.Single(events).Summary);
    }

    [Fact]
    public void CustomerSeesServiceWithProviderName()
    {
        var events = Project([Appointment(AppointmentStatus.Booked)], Customer);

        Assert.Equal("Yoga with Ana Coach", Assert.Single(events).Summary);
    }

    [Theory]
    [InlineData(AppointmentStatus.Requested, IcsEventStatus.Tentative, "Pending: Yoga with Luis Client")]
    [InlineData(AppointmentStatus.Booked, IcsEventStatus.Confirmed, "Yoga with Luis Client")]
    [InlineData(AppointmentStatus.Completed, IcsEventStatus.Confirmed, "Yoga with Luis Client")]
    [InlineData(AppointmentStatus.RescheduleRequested, IcsEventStatus.Confirmed, "Yoga with Luis Client")]
    [InlineData(AppointmentStatus.Cancelled, IcsEventStatus.Cancelled, "Cancelled: Yoga with Luis Client")]
    public void StatusMapsToIcsStatusAndTitle(AppointmentStatus status, IcsEventStatus expected, string summary)
    {
        var calendarEvent = Assert.Single(Project([Appointment(status)], Provider));

        Assert.Equal(expected, calendarEvent.Status);
        Assert.Equal(summary, calendarEvent.Summary);
    }

    [Fact]
    public void SpanishTitlesWhenTheFeedIsSpanish()
    {
        var events = CalendarFeedProjection.Project(
            [Appointment(AppointmentStatus.Requested)], Provider, Names, "es-MX", Now);

        Assert.Equal("Pendiente: Yoga con Luis Client", Assert.Single(events).Summary);
    }

    [Fact]
    public void MissingServiceAndNameFallBack()
    {
        var appointment = Appointment(AppointmentStatus.Booked);
        appointment.ServiceName = null;

        var events = CalendarFeedProjection.Project([appointment], Provider, new Dictionary<string, string>(), "en", Now);

        Assert.Equal("Appointment", Assert.Single(events).Summary);
    }

    [Fact]
    public void PendingRescheduleStaysAtOriginalTimeAndSaysSo()
    {
        var appointment = Appointment(AppointmentStatus.RescheduleRequested);
        appointment.ProposedStart = appointment.Start.AddDays(2);

        var calendarEvent = Assert.Single(Project([appointment], Provider));

        Assert.Equal(appointment.Start, calendarEvent.StartUtc);
        Assert.Contains("new time has been proposed", calendarEvent.Description);
    }

    [Fact]
    public void NoEmailAddressAppearsAnywhere()
    {
        var events = Project([Appointment(AppointmentStatus.Booked)], Provider);
        var ics = IcsWriter.Write(CalendarFeedProjection.CalendarName, events, Now);

        Assert.DoesNotContain("@example.com", ics);
    }

    [Fact]
    public void DayOffRowsAndOutOfWindowRowsAreExcluded()
    {
        var dayOff = Appointment(AppointmentStatus.Booked);
        dayOff.DayOff = true;
        var tooOld = Appointment(AppointmentStatus.Completed, Now.AddDays(-61));
        var tooFar = Appointment(AppointmentStatus.Booked, Now.AddDays(366));
        var kept = Appointment(AppointmentStatus.Booked, Now.AddDays(-59));

        var events = Project([dayOff, tooOld, tooFar, kept], Provider);

        Assert.Equal(CalendarFeedProjection.Uid(kept), Assert.Single(events).Uid);
    }

    [Fact]
    public void CapKeepsTheEventsNearestNowAndSortsByStart()
    {
        var appointments = Enumerable.Range(0, CalendarFeedProjection.MaxEvents + 50)
            .Select(i => Appointment(AppointmentStatus.Booked, Now.AddHours(i)))
            .ToList();

        var events = Project(appointments, Provider);

        Assert.Equal(CalendarFeedProjection.MaxEvents, events.Count);
        Assert.Equal(Now, events[0].StartUtc);
        Assert.Equal(events.OrderBy(e => e.StartUtc).Select(e => e.Uid), events.Select(e => e.Uid));
    }

    [Fact]
    public void UidIsStablePerAppointment()
    {
        var appointment = Appointment(AppointmentStatus.Booked);

        Assert.Equal(Assert.Single(Project([appointment], Provider)).Uid, Assert.Single(Project([appointment], Customer)).Uid);
        Assert.EndsWith("@agendame", CalendarFeedProjection.Uid(appointment));
    }

    private static IReadOnlyList<IcsEvent> Project(IEnumerable<AppointmentEntity> appointments, string owner) =>
        CalendarFeedProjection.Project(appointments, owner, Names, "en", Now);

    private static AppointmentEntity Appointment(AppointmentStatus status, DateTime? start = null)
    {
        var begins = start ?? Now.AddDays(1);
        return new AppointmentEntity
        {
            Identifier = Guid.NewGuid().ToString(),
            EmailProvider = Provider,
            EmailCustomer = Customer,
            Start = begins,
            End = begins.AddHours(1),
            AppointmentStatus = status,
            ServiceName = "Yoga"
        };
    }
}
