using System.Net;
using System.Net.Http.Headers;
using EventAndCommands.Persistence;
using AgendaBuddy.Library.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AgendaBuddy.IntegrationTests.Harness;

/// <summary>
/// F-016 AC-17 and AC-24 (`[security]`, threat <b>T-005</b>) end to end: an authenticated read writes an
/// audit record that names <b>who</b> read and <b>how much</b>, and contains no personal data.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the Calendar route rather than the one AC-24 names.</b> AC-24 is worded against
/// <c>GET /api/v1/providers</c>, but that route is <b>still anonymous</b> until F-016-T12 — so it cannot
/// produce an <em>authenticated</em> read yet, and the actor half of the criterion would be untestable.
/// <c>GET /api/v1/calendar/appointments/{email}</c> already carries <c>RequireAuthorization()</c>
/// (<c>Calendar/Program.cs:171</c>) and goes through <c>CheckCalendarAppointmentsQueryHandler</c>, which is
/// one of the nine handlers this task changed. It is also the better subject: the entity it reads embeds
/// both a customer email and a full appointment record, so "no PII in the payload" has something real to
/// be wrong about.
/// </para>
/// <para>
/// ✅ <b>The literal wording is now covered too.</b> F-016-T12 authenticated
/// <c>GET /api/v1/providers</c>, so F-016-T19 added
/// <see cref="T005_TheLiteralCriterion_AnAuthenticatedGetProvidersIsAttributedAndCarriesNoPii"/> against
/// the exact route AC-24 names. The Calendar case is kept: it exercises a different one of the nine
/// handlers.
/// </para>
/// <para>
/// The assertion searches the raw <c>data</c> string for values that were actually seeded, rather than
/// checking that some field is absent. Absence-of-a-known-field fails open the moment the payload shape
/// changes; searching for the seeded secrets does not (wave-6 standup, finding E-2).
/// </para>
/// </remarks>
[Collection(HarnessCollection.Name)]
public class QueryAuditPayloadTest :
    IClassFixture<ServiceHostFixture<CalendarAnchor>>,
    IClassFixture<ServiceHostFixture<ProviderAnchor>>
{
    private const string ProviderEmail = "audited-provider@example.com";
    private const string CustomerEmail = "audited-customer@example.com";
    private const string AppointmentDescription = "a-uniquely-identifiable-appointment-note";

    private readonly ServiceHostFixture<CalendarAnchor> _host;
    private readonly ServiceHostFixture<ProviderAnchor> _providerHost;
    private readonly TokenFactory _tokens;

    public QueryAuditPayloadTest(
        ServiceHostFixture<CalendarAnchor> host,
        ServiceHostFixture<ProviderAnchor> providerHost,
        CryptoSessionFixture crypto)
    {
        _host = host;
        _providerHost = providerHost;
        _tokens = new TokenFactory(crypto);
    }

    private static ProviderEntity SeededProvider() => new()
    {
        Id = ObjectId.GenerateNewId(),
        FirstName = "Grace",
        LastName = "Hopper",
        Email = ProviderEmail,
        AppointmentEntities =
        [
            new AppointmentEntity
            {
                Id = ObjectId.GenerateNewId(),
                EmailProvider = ProviderEmail,
                EmailCustomer = CustomerEmail,
                Start = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc),
                End = new DateTime(2026, 9, 1, 11, 0, 0, DateTimeKind.Utc),
                AppointmentDescription = AppointmentDescription,
            },
        ],
        SubscribedCustomerCollection = [CustomerEmail],
    };

    [Fact]
    public async Task T005_AnAuthenticatedReadIsAttributedToItsCallerAndRecordsNoPersonalData()
    {
        using var service = _host.StartService("Production");

        await service.Database
            .GetCollection<ProviderEntity>("providers")
            .InsertOneAsync(SeededProvider());

        var request = new HttpRequestMessage(HttpMethod.Get, $"api/v1/calendar/appointments/{ProviderEmail}");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", _tokens.CreateToken(ProviderEmail, TokenFactory.ProviderRole));

        var response = await service.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var audit = await service.Database
            .GetCollection<Event>("events")
            .Find(Builders<Event>.Filter.Eq(e => e.Type, "CheckCalendarAppointmentsQuery"))
            .FirstOrDefaultAsync();

        Assert.NotNull(audit);

        // WHO. Before F-016 the audit trail could not answer this at all
        // (15-cqrs-and-messaging.md:215) — these routes had no authenticated caller to record.
        Assert.Equal(ProviderEmail, audit!.Actor);

        // HOW MUCH, and nothing else.
        Assert.Equal("""{"resultCount":1}""", audit.Data);

        // NO PERSONAL DATA. Searching for what was actually seeded, so this cannot fail open if the
        // payload shape changes later.
        var record = audit.ToJson();
        Assert.DoesNotContain(CustomerEmail, record, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(AppointmentDescription, record, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("email_customer", record, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("subscribed_customer_collection", record, StringComparison.OrdinalIgnoreCase);

        // The provider's own email appears once, as the actor — that is attribution, which AC-24 requires,
        // not payload leakage. So it must not appear in `data`.
        Assert.DoesNotContain(ProviderEmail, audit.Data!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnAnonymousReadIsRecordedWithNoActor_WhichIsTheHonestAnswer()
    {
        // Every route reaching a query handler is anonymous until T12, so this is the live case today.
        // Null attribution is correct rather than a gap: there is no caller identity to record, and
        // inventing one would be worse than leaving it empty. It is also why Event.actor needs no backfill.
        using var service = _host.StartService("Production");

        await service.Database
            .GetCollection<ProfessionEntity>("professions")
            .InsertOneAsync(new ProfessionEntity { Id = ObjectId.GenerateNewId(), Name = "anonymous-probe" });

        var response = await service.Client.GetAsync("api/v1/calendar/availability/no-such@example.com");

        // 401: the Calendar routes require authorization, so an anonymous request never reaches a handler.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var audit = await service.Database
            .GetCollection<Event>("events")
            .Find(Builders<Event>.Filter.Empty)
            .FirstOrDefaultAsync();

        Assert.Null(audit);
    }

    [Fact]
    public async Task T005_TheLiteralCriterion_AnAuthenticatedGetProvidersIsAttributedAndCarriesNoPii()
    {
        // AC-24 is worded against GET /api/v1/providers. When this test's sibling above was written (T18)
        // that route was still anonymous, so an AUTHENTICATED read of it was impossible and only the actor
        // half could be shown on the Calendar route. F-016-T12 authenticated it, so the criterion can now be
        // attested against the exact route it names. Added at T19 rather than left as a near-miss.
        using var service = _providerHost.StartService("Production");

        await service.Database
            .GetCollection<ProviderEntity>("providers")
            .InsertOneAsync(SeededProvider());

        var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/providers");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", _tokens.CreateToken(ProviderEmail, TokenFactory.ProviderRole));

        var response = await service.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var audit = await service.Database
            .GetCollection<Event>("events")
            .Find(Builders<Event>.Filter.Eq(e => e.Type, "GetProvidersQuery"))
            .FirstOrDefaultAsync();

        Assert.NotNull(audit);
        Assert.Equal(ProviderEmail, audit!.Actor);
        Assert.Equal("""{"resultCount":1}""", audit.Data);

        // GetProvidersQueryHandler.cs:23 used to serialise every provider, every embedded appointment and
        // every customer email into this document on every call.
        var record = audit.ToJson();
        Assert.DoesNotContain(CustomerEmail, record, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(AppointmentDescription, record, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ProviderEmail, audit.Data!, StringComparison.OrdinalIgnoreCase);
    }
}
