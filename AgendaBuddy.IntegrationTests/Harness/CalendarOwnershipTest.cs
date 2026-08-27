using System.Net;
using System.Net.Http.Headers;
using AgendaBuddy.Library.Entities;
using MongoDB.Bson;

namespace AgendaBuddy.IntegrationTests.Harness;

/// <summary>
/// F-016 AC-10 and AC-25 (`[security]`, threat <b>T-006</b>): both Calendar routes are ownership-guarded,
/// and the guard runs <b>before</b> the cache read.
/// </summary>
/// <remarks>
/// <para>
/// <b>The IDOR this feature exists to close.</b> Both routes call <c>RequireAuthorization()</c> but never
/// <c>OwnershipGuard</c>, and a valid token proves the caller is <em>somebody</em>, not that <c>{email}</c>
/// is theirs. Any registered user could read any provider's full appointment list, including every customer
/// email in it. Every sibling service guards (<c>Provider:213</c>, <c>Customer:171</c>,
/// <c>Services:153,:177</c>); Calendar is the one family that forgot — and nothing could catch it, because
/// there was no integration test in the solution (<c>11-testing.md:148</c>).
/// </para>
/// <para>
/// <b>DESIGN INVARIANT: the guard must execute before the cache read.</b> Both routes cache under
/// <c>availability-{email}</c> / <c>appointments-{email}</c> — keyed on the request <em>subject</em>, never
/// the <em>caller</em>. Today that is safe by accident: with no guard, every authenticated caller is
/// entitled to every entry. Adding the guard keeps it safe <b>only because the guard runs first</b>. Anyone
/// who later reorders these lines, extracts a helper, or caches the <em>response</em> instead of the
/// <em>data</em> creates a cross-tenant leak — and F-019/F-020 will rewrite these exact files. Hence
/// <see cref="T006_AWarmCacheIsNotServedToADifferentPrincipal"/>, which fails if the ordering is ever
/// inverted.
/// </para>
/// <para>
/// ⚠️ <b>The assertion is "not 200-with-data", not "exactly 403"</b>, and that is deliberate.
/// <c>CacheAside</c> has no test at all and returns <c>default!</c> on a 500 ms lock timeout, which surfaces
/// as a spurious 404 (<c>11-testing.md:90</c>). A strict 403 assertion would flake under cache-lock
/// contention and send someone chasing a phantom authorization bug. What matters for T-006 is that the
/// second caller does not receive the first caller's data.
/// </para>
/// </remarks>
[Collection(HarnessCollection.Name)]
public class CalendarOwnershipTest : IClassFixture<ServiceHostFixture<CalendarAnchor>>
{
    private const string Owner = "calendar-owner@example.com";
    private const string Intruder = "intruder@example.com";
    private const string CustomerInTheBook = "a-real-client@example.com";

    private readonly ServiceHostFixture<CalendarAnchor> _host;
    private readonly TokenFactory _tokens;

    public CalendarOwnershipTest(ServiceHostFixture<CalendarAnchor> host, CryptoSessionFixture crypto)
    {
        _host = host;
        _tokens = new TokenFactory(crypto);
    }

    private async Task<ServiceHost> StartWithTheOwnersCalendar()
    {
        var service = _host.StartService("Production");

        await service.Database.GetCollection<ProviderEntity>("providers").InsertOneAsync(new ProviderEntity
        {
            Id = ObjectId.GenerateNewId(),
            FirstName = "Grace",
            LastName = "Hopper",
            Email = Owner,
            AppointmentEntities =
            [
                new AppointmentEntity
                {
                    Id = ObjectId.GenerateNewId(),
                    EmailProvider = Owner,
                    EmailCustomer = CustomerInTheBook,
                    Start = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc),
                    End = new DateTime(2026, 9, 1, 11, 0, 0, DateTimeKind.Utc),
                },
            ],
        });

        return service;
    }

    private HttpRequestMessage Read(string route, string callerSubject)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, route);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", _tokens.CreateToken(callerSubject, TokenFactory.ProviderRole));
        return request;
    }

    public static TheoryData<string> BothRoutes() => new()
    {
        $"api/v1/calendar/availability/{Owner}",
        $"api/v1/calendar/appointments/{Owner}",
    };

    [Theory]
    [MemberData(nameof(BothRoutes))]
    public async Task AC10_ADifferentAuthenticatedPrincipalGets403(string route)
    {
        using var service = await StartWithTheOwnersCalendar();

        var response = await service.Client.SendAsync(Read(route, Intruder));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(BothRoutes))]
    public async Task AC10_TheOwnerIsNotRefused(string route)
    {
        // The control. Without it, a guard that refused everybody would satisfy both assertions above --
        // and this route family's whole problem was that nothing checked its behaviour end to end.
        using var service = await StartWithTheOwnersCalendar();

        var response = await service.Client.SendAsync(Read(route, Owner));

        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task T006_AWarmCacheIsNotServedToADifferentPrincipal()
    {
        // The regression test for the design invariant. The cache key is derived from {email} alone, so if
        // the guard is ever moved after the cache read, the intruder receives the owner's warm entry and
        // this fails. It is the only thing standing between that reordering and a cross-tenant leak.
        using var service = await StartWithTheOwnersCalendar();
        var route = $"api/v1/calendar/appointments/{Owner}";

        // Warm it as the owner, and confirm it really is warm — otherwise the test proves nothing.
        var ownersRead = await service.Client.SendAsync(Read(route, Owner));
        Assert.Equal(HttpStatusCode.OK, ownersRead.StatusCode);
        Assert.Contains(CustomerInTheBook, await ownersRead.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        var intrudersRead = await service.Client.SendAsync(Read(route, Intruder));
        var body = await intrudersRead.Content.ReadAsStringAsync();

        // "NOT 200-with-data" rather than "exactly 403": see the remarks on this class. A 404 from a
        // CacheAside lock timeout is an acceptable outcome for T-006; a 200 carrying the owner's client
        // email is not.
        Assert.NotEqual(HttpStatusCode.OK, intrudersRead.StatusCode);
        Assert.DoesNotContain(CustomerInTheBook, body, StringComparison.OrdinalIgnoreCase);
    }
}
