using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AgendaBuddy.Library.Entities;
using Microsoft.AspNetCore.Mvc.Testing;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AgendaBuddy.IntegrationTests.Harness;

/// <summary>
/// A request's JWT crosses the gateway unmodified. Booking's own auth/ownership
/// pipeline is the judge — this class asserts that going through the gateway produces
/// exactly the outcomes <c>PaymentsAndStatusTest</c> already proves for a direct call, using the SAME
/// route, the SAME database, and the SAME <see cref="TokenFactory"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this doubles as a real proof, not a restatement of the design.</b> The gateway client and the
/// direct client (<c>bookingHost.Client</c>) both ultimately hit the same <see cref="Booking"/>
/// <c>TestServer</c> and the same MongoDB database — the gateway is a real extra HTTP hop through YARP's
/// routing, its header transforms, and its failure-translation transform,
/// not a stand-in. If YARP dropped, re-signed, or corrupted the <c>Authorization</c> header, or if
/// forwarding somehow bypassed Booking's pipeline, the assertions below would fail exactly the way a
/// tampered-token test fails directly against Booking — they do not, which is AC3 and AC4 proved rather
/// than assumed. See <see cref="GatewayToRealServiceHarness"/> for how "through the gateway" is wired
/// without a real TCP socket.
/// </para>
/// </remarks>
[Collection(HarnessCollection.Name)]
public class GatewayJwtPassthroughTest(ServiceHostFixture<BookingAnchor> host, CryptoSessionFixture crypto)
    : IClassFixture<ServiceHostFixture<BookingAnchor>>
{
    private const string Provider = "gateway-jwt-provider@example.com";
    private const string Customer = "gateway-jwt-customer@example.com";
    private const string Appointment = "gateway-jwt-appointment";

    private readonly TokenFactory _tokens = new(crypto);

    private async Task<(ServiceHost Backend, WebApplicationFactory<GatewayAnchor> GatewayFactory, HttpClient Gateway)>
        StartAsync()
    {
        var backend = host.StartService("Production");

        var appointment = new AppointmentEntity
        {
            Id = ObjectId.GenerateNewId(),
            Identifier = Appointment,
            EmailProvider = Provider,
            EmailCustomer = Customer,
            Start = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc),
            End = new DateTime(2026, 9, 1, 11, 0, 0, DateTimeKind.Utc),
            AppointmentStatus = AppointmentStatus.Requested
        };
        await backend.Database.GetCollection<AppointmentEntity>("appointments").InsertOneAsync(appointment);
        await backend.Database.GetCollection<ProviderEntity>("providers").InsertOneAsync(new ProviderEntity
        {
            Id = ObjectId.GenerateNewId(),
            FirstName = "Gateway",
            LastName = "Provider",
            Email = Provider,
            AppointmentEntities = [appointment]
        });

        var gatewayFactory = GatewayToRealServiceHarness.CreateFactory("booking", backend.Server);
        var gatewayClient = gatewayFactory.CreateClient();

        return (backend, gatewayFactory, gatewayClient);
    }

    private static HttpRequestMessage StatusRequest(string? bearerToken, object body)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post, $"api/v1/booking/appointments/{Appointment}/status")
        {
            Content = JsonContent.Create(body)
        };

        if (bearerToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        return request;
    }

    private static async Task<AppointmentStatus> StoredStatusAsync(ServiceHost backend) =>
        (await backend.Database.GetCollection<AppointmentEntity>("appointments")
            .Find(Builders<AppointmentEntity>.Filter.Eq(a => a.Identifier, Appointment)).SingleAsync())
        .AppointmentStatus;

    // ── AC3: a valid JWT reaches the destination unmodified, and is honoured exactly as a direct call ──

    [Fact]
    public async Task AC3_AValidJwt_TransitionsTheAppointment_ExactlyAsADirectCallWould()
    {
        var (backend, gatewayFactory, gateway) = await StartAsync();
        using var _ = backend;
        using var __ = gatewayFactory;
        using var ___ = gateway;

        // PaymentsAndStatusTest.AC14 proves a direct POST from the Customer role to "Booked" returns 200
        // and updates both copies. Same route, same token shape, same expectation — reached the other
        // way in.
        var response = await gateway.SendAsync(
            StatusRequest(_tokens.CreateToken(Customer, TokenFactory.CustomerRole), new { status = "Booked" }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(AppointmentStatus.Booked, await StoredStatusAsync(backend));
    }

    [Fact]
    public async Task AC3_AValidJwtForTheWrongRole_IsForbidden_ExactlyAsADirectCallWould()
    {
        // A Customer completing their own appointment breaks the provider-only rule. If the
        // gateway altered the token's role claim in transit, this would come back 200 instead of 403 —
        // the JWT's role reaching Booking unmodified is exactly what this pins.
        var (backend, gatewayFactory, gateway) = await StartAsync();
        using var _ = backend;
        using var __ = gatewayFactory;
        using var ___ = gateway;

        await backend.Database.GetCollection<AppointmentEntity>("appointments").FindOneAndUpdateAsync(
            Builders<AppointmentEntity>.Filter.Eq(a => a.Identifier, Appointment),
            Builders<AppointmentEntity>.Update.Set(a => a.AppointmentStatus, AppointmentStatus.Booked));

        var response = await gateway.SendAsync(StatusRequest(
            _tokens.CreateToken(Customer, TokenFactory.CustomerRole), new { status = "Completed" }));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(AppointmentStatus.Booked, await StoredStatusAsync(backend));
    }

    [Fact]
    public async Task AC3_AValidJwtForAStranger_IsForbidden_ExactlyAsADirectCallWould()
    {
        // The token's SUBJECT reaching Booking unmodified: a correctly-signed token for someone who is
        // neither participant is still refused by ownership, not accepted because the signature checked out.
        var (backend, gatewayFactory, gateway) = await StartAsync();
        using var _ = backend;
        using var __ = gatewayFactory;
        using var ___ = gateway;

        var response = await gateway.SendAsync(StatusRequest(
            _tokens.CreateToken("stranger@example.com", TokenFactory.ProviderRole), new { status = "Booked" }));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(AppointmentStatus.Requested, await StoredStatusAsync(backend));
    }

    // ── AC4: anonymous / invalid JWT gets the same refusal a direct call would ─────────────────────────

    [Fact]
    public async Task AC4_AnAnonymousRequest_Gets401_ExactlyAsADirectCallWould()
    {
        // PaymentsAndStatusTest.AC8: every status/payment route refuses an anonymous caller with 401.
        var (backend, gatewayFactory, gateway) = await StartAsync();
        using var _ = backend;
        using var __ = gatewayFactory;
        using var ___ = gateway;

        var response = await gateway.SendAsync(StatusRequest(bearerToken: null, new { status = "Booked" }));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(AppointmentStatus.Requested, await StoredStatusAsync(backend));
    }

    [Fact]
    public async Task AC4_AnExpiredJwt_Gets401_ExactlyAsADirectCallWould()
    {
        var (backend, gatewayFactory, gateway) = await StartAsync();
        using var _ = backend;
        using var __ = gatewayFactory;
        using var ___ = gateway;

        var response = await gateway.SendAsync(StatusRequest(
            _tokens.CreateExpiredToken(Customer, TokenFactory.CustomerRole), new { status = "Booked" }));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(AppointmentStatus.Requested, await StoredStatusAsync(backend));
    }

    [Fact]
    public async Task AC4_ATamperedJwt_Gets401_ExactlyAsADirectCallWould()
    {
        // A valid token whose signature no longer matches its content — the token that would prove the
        // gateway is checking nothing itself, only forwarding, and Booking is doing the real work.
        var (backend, gatewayFactory, gateway) = await StartAsync();
        using var _ = backend;
        using var __ = gatewayFactory;
        using var ___ = gateway;

        var tampered = _tokens.CreateToken(Customer, TokenFactory.CustomerRole);
        tampered = tampered[..^2] + (tampered[^2] == 'A' ? "B" : "A") + tampered[^1];

        var response = await gateway.SendAsync(StatusRequest(tampered, new { status = "Booked" }));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(AppointmentStatus.Requested, await StoredStatusAsync(backend));
    }
}
