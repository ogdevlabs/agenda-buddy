using System.Xml.Linq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Views;

/// <summary>
/// Every list of appointments has to be able to OPEN one.
/// </summary>
/// <remarks>
/// <para>
/// <b>This exists because the calendar's rows were inert.</b> Reschedule and cancel live on
/// <c>AppointmentDetailPage</c>, and the only way into that page was a dashboard row — so a provider looking at
/// their own calendar could see a session and had no way to change it, and a session further out than the
/// dashboard's page showed could not be reached at all. Nothing failed: the rows rendered perfectly and simply
/// did nothing when tapped.
/// </para>
/// <para>
/// Read as XML off disk, like the other structural view tests, because XAML is not compiled on the
/// <c>net10.0</c> slice.
/// </para>
/// </remarks>
public class AppointmentReachabilityTest
{
    /// <summary>Views that list appointments and must therefore be able to open one.</summary>
    private static readonly string[] AppointmentListViews =
    [
        "CalendarPage.xaml",
        "DashboardPage.xaml"
    ];

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "agenda-buddy.sln")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        return directory!.FullName;
    }

    private static XDocument Load(string viewName) =>
        XDocument.Load(Path.Combine(RepoRoot(), "AgendaBuddy.MobileApp", "Views", viewName));

    /// <summary>
    /// A row somebody can tap is the only way to reach the actions on an appointment.
    /// </summary>
    [Theory]
    [InlineData("CalendarPage.xaml")]
    [InlineData("DashboardPage.xaml")]
    public void AViewThatListsAppointmentsCanOpenOne(string viewName)
    {
        var root = Load(viewName).Root;
        Assert.NotNull(root);

        var taps = root!.DescendantsAndSelf()
            .Count(element => element.Name.LocalName == "TapGestureRecognizer");

        Assert.True(
            taps > 0,
            $"{viewName} lists appointments but has no TapGestureRecognizer, so no row can be opened. "
            + "Reschedule and cancel live on AppointmentDetailPage, and a row that cannot navigate there "
            + "silently withholds every action on the appointment.");
    }

    /// <summary>
    /// The route has to be the one the Shell actually registers.
    /// </summary>
    /// <remarks>
    /// Both callers go through <c>AppointmentNavigation.Route</c> rather than a literal, so this asserts the
    /// constant matches what <c>AppShell</c> registers — a mismatch is a page that never opens, with nothing
    /// failing at build time.
    /// </remarks>
    [Fact]
    public void TheAppointmentRouteIsRegisteredOnTheShell()
    {
        var shell = File.ReadAllText(
            Path.Combine(RepoRoot(), "AgendaBuddy.MobileApp", "AppShell.xaml.cs"));

        Assert.Contains(
            $"RegisterRoute(\"{MobileApp.Infrastructure.AppointmentNavigation.Route}\"", shell);
    }

    /// <summary>
    /// Both callers build the SAME query, and it must carry <c>scheduledAt</c>.
    /// </summary>
    /// <remarks>
    /// <c>scheduledAt</c> is the tell <c>AppointmentDetailPage</c> uses to decide whether the caller had the
    /// appointment in hand. Omitting it makes the navigation look identifier-only, which sends the page down its
    /// fetch-and-maybe-error path instead of rendering what the caller already knew.
    /// </remarks>
    [Fact]
    public void TheSharedQueryCarriesEverythingTheDetailPageReads()
    {
        var query = MobileApp.Infrastructure.AppointmentNavigation.BuildQuery(
            new MobileApp.Models.AppointmentDetail
            {
                Id = "abc123",
                CustomerEmail = "ada@example.com",
                ScheduledAt = new DateTime(2026, 9, 20, 16, 0, 0, DateTimeKind.Local),
                ServiceName = "1:1 Strength Session",
                ServiceDurationMinutes = 45
            });

        foreach (var key in new[]
                 {
                     "appointmentId", "customerEmail", "customerName", "customerPhone", "providerName",
                     "displayName", "scheduledAt", "status", "serviceName", "serviceDurationMinutes",
                     "customerNotes"
                 })
        {
            Assert.True(query.ContainsKey(key), $"the appointment query is missing '{key}'");
        }

        // The invariant is that it ROUND-TRIPS, not any particular string: a local instant carries its offset,
        // which is exactly what stops the device's culture or zone reinterpreting it on the way back in.
        var scheduledAt = Assert.IsType<string>(query["scheduledAt"]);
        var parsed = DateTime.Parse(
            scheduledAt, null, System.Globalization.DateTimeStyles.RoundtripKind);

        Assert.Equal(new DateTime(2026, 9, 20, 16, 0, 0, DateTimeKind.Local), parsed);
    }

    /// <summary>
    /// The two callers hold different types, and both have to be buildable — otherwise one of them goes back to
    /// hand-assembling the dictionary, which is what the shared builder exists to prevent.
    /// </summary>
    [Fact]
    public void BothCallersTypesAreSupported()
    {
        var fromSummary = MobileApp.Infrastructure.AppointmentNavigation.BuildQuery(
            new MobileApp.Models.AppointmentSummary { Id = "abc123", ScheduledAt = DateTime.Now });
        var fromDetail = MobileApp.Infrastructure.AppointmentNavigation.BuildQuery(
            new MobileApp.Models.AppointmentDetail { Id = "abc123", ScheduledAt = DateTime.Now });

        Assert.Equal(fromSummary.Keys.OrderBy(key => key), fromDetail.Keys.OrderBy(key => key));
    }

    /// <summary>
    /// Neither caller may hand-assemble the query any more.
    /// </summary>
    /// <remarks>
    /// A second dictionary is a second chance to omit a key, and the omission degrades silently. This fails if
    /// either page starts building one again.
    /// </remarks>
    [Theory]
    [InlineData("CalendarPage.xaml.cs")]
    [InlineData("DashboardPage.xaml.cs")]
    public void NeitherCallerBuildsTheQueryByHand(string codeBehind)
    {
        var source = File.ReadAllText(
            Path.Combine(RepoRoot(), "AgendaBuddy.MobileApp", "Views", codeBehind));

        Assert.Contains("AppointmentNavigation.BuildQuery", source);
        Assert.DoesNotContain("[\"appointmentId\"] =", source);
    }
}
