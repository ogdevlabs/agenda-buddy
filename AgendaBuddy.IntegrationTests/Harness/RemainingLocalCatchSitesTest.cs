using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace AgendaBuddy.IntegrationTests.Harness;

/// <summary>
/// For the five local <c>ForbiddenException</c> catch sites <c>LocalCatchUnaffectedTest</c> does
/// not reach — closes review finding <b>I-3</b>.
/// </summary>
/// <remarks>
/// <para>
/// AC-14 requires that the hand-written catch sites "still return exactly one 403, no double-handling, no
/// changed body". T08 removed one of the seven (Customer's, for AC-13), leaving six.
/// <c>LocalCatchUnaffectedTest</c> covered <b>one</b> of those six — Provider <c>:273</c>. The Party Review
/// flagged the other five as unverified end-to-end, which mattered because T08 inserted a new
/// exception-handler middleware into all six services' pipelines:
/// </para>
/// <list type="bullet">
/// <item><c>Booking/Program.cs:125</c> — <c>POST /api/v1/booking/appointments</c></item>
/// <item><c>Booking/Program.cs:149</c> — <c>PUT /api/v1/booking/appointments/</c></item>
/// <item><c>Booking/Program.cs:174</c> — <c>DELETE /api/v1/booking/appointments/</c></item>
/// <item><c>Services/Program.cs:153</c> — <c>PUT /api/v1/services/{email}</c></item>
/// <item><c>Services/Program.cs:177</c> — <c>PATCH /api/v1/services/{email}</c></item>
/// </list>
/// <para>
/// All five share the shape of the one already covered: <c>MiniValidator</c> first, then the guard, then
/// <c>catch (ForbiddenException) → TypedResults.Forbid()</c>. So <b>the request bodies must be valid</b> or
/// validation returns 400 and the guard is never reached — the same trap recorded on
/// <c>AuthFailurePathTest</c>.
/// </para>
/// <para>
/// The assertion is the same as <c>LocalCatchUnaffectedTest</c>'s: a single well-formed ProblemDetails body
/// with <see cref="ForbiddenContract.Properties"/>. A second write over an already-started response would
/// either throw or leave two concatenated documents, and <c>JsonDocument.Parse</c> rejects trailing content —
/// which is what "exactly one 403" is observable as from outside HTTP.
/// </para>
/// </remarks>
[Collection(HarnessCollection.Name)]
public class RemainingLocalCatchSitesTest :
    IClassFixture<ServiceHostFixture<BookingAnchor>>,
    IClassFixture<ServiceHostFixture<ServicesAnchor>>
{
    private const string Owner = "route-owner@example.com";
    private const string OtherParty = "the-other-party@example.com";
    private const string Stranger = "stranger@example.com";

    private readonly ServiceHostFixture<BookingAnchor> _booking;
    private readonly ServiceHostFixture<ServicesAnchor> _services;
    private readonly TokenFactory _tokens;

    public RemainingLocalCatchSitesTest(
        ServiceHostFixture<BookingAnchor> booking,
        ServiceHostFixture<ServicesAnchor> services,
        CryptoSessionFixture crypto)
    {
        _booking = booking;
        _services = services;
        _tokens = new TokenFactory(crypto);
    }

    /// <summary>A valid appointment body — neither party is the caller, so the guard refuses.</summary>
    private static object AppointmentBody() => new
    {
        EmailProvider = Owner,
        EmailCustomer = OtherParty,
        Start = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc),
        End = new DateTime(2026, 9, 1, 11, 0, 0, DateTimeKind.Utc),
    };

    /// <summary>A valid service-catalogue body for the Services routes.</summary>
    private static object[] ServicesBody() =>
    [
        new { Name = "60-min session", Description = "a valid service", Fee = 65m },
    ];

    public static TheoryData<string, string> BookingCatchSites() => new()
    {
        { "POST", "api/v1/booking/appointments" },
        { "PUT", "api/v1/booking/appointments/" },
        { "DELETE", "api/v1/booking/appointments/" },
    };

    public static TheoryData<string, string> ServicesCatchSites() => new()
    {
        { "PUT", $"api/v1/services/{Owner}" },
        { "PATCH", $"api/v1/services/{Owner}" },
    };

    private HttpRequestMessage Refused(string method, string route, object body) =>
        new(new HttpMethod(method), route)
        {
            Content = JsonContent.Create(body),
            Headers =
            {
                // A valid token for somebody with no claim on the resource.
                Authorization = new AuthenticationHeaderValue(
                    "Bearer", _tokens.CreateToken(Stranger, TokenFactory.ProviderRole)),
            },
        };

    private static async Task AssertExactlyOneForbidden(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        using var problem = JsonDocument.Parse(body);

        Assert.Equal(
            ForbiddenContract.Properties,
            problem.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));

        Assert.Equal(403, problem.RootElement.GetProperty("status").GetInt32());

        // The exception-message-suppression guarantee holds on these paths too — inherited, not implemented.
        Assert.DoesNotContain("ForbiddenException", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("   at ", body, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(BookingCatchSites))]
    public async Task AC14_BookingsThreeCatchSitesStillReturnExactlyOne403(string method, string route)
    {
        using var service = _booking.StartService("Production");

        var response = await service.Client.SendAsync(Refused(method, route, AppointmentBody()));

        await AssertExactlyOneForbidden(response);
    }

    [Theory]
    [MemberData(nameof(ServicesCatchSites))]
    public async Task AC14_ServicesTwoCatchSitesStillReturnExactlyOne403(string method, string route)
    {
        using var service = _services.StartService("Production");

        var response = await service.Client.SendAsync(Refused(method, route, ServicesBody()));

        await AssertExactlyOneForbidden(response);
    }

    [Fact]
    public async Task AC14_ThePartyWhoOwnsTheAppointmentIsNotRefused()
    {
        // The control. Booking guards with AssertOwnerAny(provider, customer), so EITHER party is entitled —
        // a guard that refused everybody would satisfy all five assertions above.
        using var service = _booking.StartService("Production");

        var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/booking/appointments")
        {
            Content = JsonContent.Create(AppointmentBody()),
            Headers =
            {
                Authorization = new AuthenticationHeaderValue(
                    "Bearer", _tokens.CreateToken(OtherParty, TokenFactory.CustomerRole)),
            },
        };

        var response = await service.Client.SendAsync(request);

        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
