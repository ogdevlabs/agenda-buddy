using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Library.Entities;
using Library.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AgendaBuddy.IntegrationTests.Harness;

/// <summary>
/// F-014 AC-6, AC-8, AC-13 … AC-17 / threats T-203 and T-205: appointment status is the server's, and a
/// payment is recorded against the appointment's real participants without charging anyone.
/// </summary>
[Collection(HarnessCollection.Name)]
public class PaymentsAndStatusTest(ServiceHostFixture<BookingAnchor> host, CryptoSessionFixture crypto)
    : IClassFixture<ServiceHostFixture<BookingAnchor>>
{
    private const string Provider = "status-provider@example.com";
    private const string Customer = "status-customer@example.com";
    private const string Stranger = "stranger@example.com";
    private const string Appointment = "appointment-status";

    private readonly TokenFactory _tokens = new(crypto);

    private async Task<ServiceHost> StartWithAnAppointmentAsync(
        AppointmentStatus status = AppointmentStatus.Requested)
    {
        var service = host.StartService("Production");

        var appointment = new AppointmentEntity
        {
            Id = ObjectId.GenerateNewId(),
            Identifier = Appointment,
            EmailProvider = Provider,
            EmailCustomer = Customer,
            Start = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc),
            End = new DateTime(2026, 9, 1, 11, 0, 0, DateTimeKind.Utc),
            AppointmentStatus = status
        };

        await service.Database.GetCollection<AppointmentEntity>("appointments").InsertOneAsync(appointment);

        // The provider document carries an embedded copy, which is what ReportingService counts from — so the
        // status route has to keep both in step, and these tests are what prove it does.
        await service.Database.GetCollection<ProviderEntity>("providers").InsertOneAsync(new ProviderEntity
        {
            Id = ObjectId.GenerateNewId(),
            FirstName = "Status",
            LastName = "Provider",
            Email = Provider,
            AppointmentEntities = [appointment]
        });

        return service;
    }

    private HttpRequestMessage Authorised(HttpMethod method, string path, string subject, string role,
        object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokens.CreateToken(subject, role));
        if (body is not null) request.Content = JsonContent.Create(body);
        return request;
    }

    private static async Task<AppointmentEntity> StoredAsync(ServiceHost service) =>
        await service.Database.GetCollection<AppointmentEntity>("appointments")
            .Find(Builders<AppointmentEntity>.Filter.Eq(a => a.Identifier, Appointment)).SingleAsync();

    private static async Task<AppointmentStatus> EmbeddedStatusAsync(ServiceHost service)
    {
        var provider = await service.Database.GetCollection<ProviderEntity>("providers")
            .Find(Builders<ProviderEntity>.Filter.Eq(p => p.Email, Provider)).SingleAsync();

        return provider.AppointmentEntities.Single(a => a.Identifier == Appointment).AppointmentStatus;
    }

    // ── Status ──────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("POST", "api/v1/booking/appointments/appointment-status/status")]
    [InlineData("POST", "api/v1/booking/appointments/appointment-status/payment")]
    [InlineData("GET", "api/v1/booking/appointments/appointment-status/payment")]
    public async Task AC8_EveryStatusAndPaymentRoute_RefusesAnAnonymousCaller(string method, string path)
    {
        using var service = await StartWithAnAppointmentAsync();

        var response = await service.Client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = JsonContent.Create(new { status = "Booked", amount = 50m })
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AC13_T203_ThePutIgnoresAClientAssertedStatus()
    {
        // THE forgery this feature closes. Before F-014, UpdateAppointmentCommandHandler:51 copied whatever
        // status arrived in the body, so a customer could mark a brand-new appointment Completed — a claim
        // that work was delivered — and the guards on AppointmentEntity never ran.
        using var service = await StartWithAnAppointmentAsync();

        var response = await service.Client.SendAsync(Authorised(
            HttpMethod.Put, "api/v1/booking/appointments/", Customer, TokenFactory.CustomerRole,
            new
            {
                identifier = Appointment,
                emailProvider = Provider,
                emailCustomer = Customer,
                start = "2026-09-01T10:00:00Z",
                end = "2026-09-01T12:00:00Z",
                // The INTEGER form, because that is what this API actually binds: no JsonStringEnumConverter
                // is registered, so `"Completed"` would fail model binding with a bare 400 and this test would
                // pass for the wrong reason — proving nothing about whether the status was ignored.
                // 2 == AppointmentStatus.Completed.
                appointmentStatus = 2
            }));

        Assert.True(response.IsSuccessStatusCode,
            $"the update itself should succeed, got {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

        // F-019-T11 (AC8) / Party Review (agenda-buddy-2hd, fixed): live confirmation the response
        // body reflects the actual persisted entity, not the client's forged submission -- the
        // handler used to echo request.AppointmentEntity verbatim, so this assertion would have
        // failed (reporting "Completed") before the fix.
        var wrapper = await response.Content.ReadFromJsonAsync<DataResponse<AppointmentEntity>>(HarnessJson.Options);
        Assert.Equal(Appointment, wrapper!.Data!.Identifier);
        Assert.Equal(AppointmentStatus.Requested, wrapper.Data.AppointmentStatus);

        var stored = await StoredAsync(service);
        Assert.Equal(AppointmentStatus.Requested, stored.AppointmentStatus);

        // And the fields the caller DOES own were applied, so this is "status is ignored", not "the whole
        // request is ignored".
        Assert.Equal(new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc), stored.End);
    }

    [Fact]
    public async Task AC14_TheTransitionRouteWalksTheGraph_AndKeepsBothCopiesInStep()
    {
        using var service = await StartWithAnAppointmentAsync();

        var booked = await service.Client.SendAsync(Authorised(
            HttpMethod.Post, $"api/v1/booking/appointments/{Appointment}/status",
            Customer, TokenFactory.CustomerRole, new { status = "Booked" }));

        Assert.Equal(HttpStatusCode.OK, booked.StatusCode);
        Assert.Equal(AppointmentStatus.Booked, (await StoredAsync(service)).AppointmentStatus);

        // The embedded copy too — updating only the collection would leave the provider's dashboard reporting
        // the old status indefinitely, because ReportingService counts from the embedded list.
        Assert.Equal(AppointmentStatus.Booked, await EmbeddedStatusAsync(service));

        var completed = await service.Client.SendAsync(Authorised(
            HttpMethod.Post, $"api/v1/booking/appointments/{Appointment}/status",
            Provider, TokenFactory.ProviderRole, new { status = "Completed" }));

        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        Assert.Equal(AppointmentStatus.Completed, (await StoredAsync(service)).AppointmentStatus);
        Assert.Equal(AppointmentStatus.Completed, await EmbeddedStatusAsync(service));
    }

    [Fact]
    public async Task AC14_AnIllegalTransitionAnswers409_AndWritesNothing()
    {
        using var service = await StartWithAnAppointmentAsync();

        var response = await service.Client.SendAsync(Authorised(
            HttpMethod.Post, $"api/v1/booking/appointments/{Appointment}/status",
            Provider, TokenFactory.ProviderRole, new { status = "Completed" }));

        // 409 rather than 400: the request is well-formed, it conflicts with the current state.
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(AppointmentStatus.Requested, (await StoredAsync(service)).AppointmentStatus);
        Assert.Equal(AppointmentStatus.Requested, await EmbeddedStatusAsync(service));
    }

    [Fact]
    public async Task AC16_T203_ACustomerCannotCompleteTheirOwnAppointment()
    {
        // Booking is a scheduling action either party can take; completing is a claim about work delivered, so
        // it is the provider's alone.
        using var service = await StartWithAnAppointmentAsync(AppointmentStatus.Booked);

        var response = await service.Client.SendAsync(Authorised(
            HttpMethod.Post, $"api/v1/booking/appointments/{Appointment}/status",
            Customer, TokenFactory.CustomerRole, new { status = "Completed" }));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(AppointmentStatus.Booked, (await StoredAsync(service)).AppointmentStatus);
    }

    [Fact]
    public async Task AStrangerCannotTouchTheStatusAtAll()
    {
        using var service = await StartWithAnAppointmentAsync();

        var response = await service.Client.SendAsync(Authorised(
            HttpMethod.Post, $"api/v1/booking/appointments/{Appointment}/status",
            Stranger, TokenFactory.ProviderRole, new { status = "Booked" }));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(AppointmentStatus.Requested, (await StoredAsync(service)).AppointmentStatus);
    }

    [Theory]
    [InlineData("Cancelled")]
    [InlineData("Confirmed")]
    [InlineData("Requested")]
    [InlineData("not-a-status")]
    public async Task StatesOutsideTheGraph_AreRefused(string target)
    {
        using var service = await StartWithAnAppointmentAsync();

        var response = await service.Client.SendAsync(Authorised(
            HttpMethod.Post, $"api/v1/booking/appointments/{Appointment}/status",
            Provider, TokenFactory.ProviderRole, new { status = target }));

        // 400 for a value that is not a status at all; 409 for one that is but has no transition into it.
        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.BadRequest, HttpStatusCode.Conflict });
        Assert.Equal(AppointmentStatus.Requested, (await StoredAsync(service)).AppointmentStatus);
    }

    [Fact]
    public async Task AC15_ABookedAppointmentCanBeCancelled_AndACompletedOneCannot()
    {
        // Discover finding F-3: cancellation used to refuse a BOOKED appointment — the state a customer
        // actually needs to cancel — and it was invisible because nothing ever set Booked. Making transitions
        // real activates the bug, so both are fixed together.
        using var booked = await StartWithAnAppointmentAsync(AppointmentStatus.Booked);

        var cancelBooked = await booked.Client.SendAsync(Authorised(
            HttpMethod.Delete, "api/v1/booking/appointments/", Customer, TokenFactory.CustomerRole,
            new
            {
                identifier = Appointment,
                emailProvider = Provider,
                emailCustomer = Customer,
                start = "2026-09-01T10:00:00Z",
                end = "2026-09-01T11:00:00Z"
            }));

        Assert.Equal(HttpStatusCode.NoContent, cancelBooked.StatusCode);
        Assert.Equal(0, await booked.Database.GetCollection<AppointmentEntity>("appointments")
            .CountDocumentsAsync(Builders<AppointmentEntity>.Filter.Eq(a => a.Identifier, Appointment)));

        using var completed = await StartWithAnAppointmentAsync(AppointmentStatus.Completed);

        var cancelCompleted = await completed.Client.SendAsync(Authorised(
            HttpMethod.Delete, "api/v1/booking/appointments/", Customer, TokenFactory.CustomerRole,
            new
            {
                identifier = Appointment,
                emailProvider = Provider,
                emailCustomer = Customer,
                start = "2026-09-01T10:00:00Z",
                end = "2026-09-01T11:00:00Z"
            }));

        Assert.NotEqual(HttpStatusCode.NoContent, cancelCompleted.StatusCode);
        Assert.Equal(1, await completed.Database.GetCollection<AppointmentEntity>("appointments")
            .CountDocumentsAsync(Builders<AppointmentEntity>.Filter.Eq(a => a.Identifier, Appointment)));
    }

    // ── Payments ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC6_AC17_APaymentIsRecorded_WithoutChargingAnyone()
    {
        using var service = await StartWithAnAppointmentAsync();

        var response = await service.Client.SendAsync(Authorised(
            HttpMethod.Post, $"api/v1/booking/appointments/{Appointment}/payment",
            Customer, TokenFactory.CustomerRole, new { amount = 50m, currency = "gbp" }));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        // F-019-T06: DataResponse<T> envelope -- the payment moved from the root to .data.
        var payment = (await response.Content.ReadFromJsonAsync<DataResponse<PaymentEntity>>(HarnessJson.Options))!.Data;
        Assert.Equal(PaymentStatus.Succeeded, payment!.Status);

        // The proof that nothing was charged, and it is in the STORED DATA rather than only in a log: Stripe
        // ids begin `pi_`, so `local_` is permanently identifiable as a payment that moved no money. A UI that
        // renders this as "Paid" is lying to a provider about their income (threat T-205).
        Assert.StartsWith(RecordingPaymentGateway.LocalIntentPrefix, payment.StripePaymentIntentId);

        // Both participants come from the STORED APPOINTMENT, never the request body.
        Assert.Equal(Provider, payment.ProviderEmail);
        Assert.Equal(Customer, payment.CustomerEmail);
        Assert.Equal(50m, payment.Amount);
    }

    [Fact]
    public async Task T205_ParticipantsComeFromTheAppointment_NotTheRequest()
    {
        using var service = await StartWithAnAppointmentAsync();

        var response = await service.Client.SendAsync(Authorised(
            HttpMethod.Post, $"api/v1/booking/appointments/{Appointment}/payment",
            Customer, TokenFactory.CustomerRole,
            new
            {
                amount = 1m,
                currency = "gbp",
                // A caller trying to record a payment against somebody else. PaymentRequest has no such
                // fields, so these are ignored rather than validated — there is nothing to trust.
                providerEmail = Stranger,
                customerEmail = Stranger
            }));

        var payment = (await response.Content.ReadFromJsonAsync<DataResponse<PaymentEntity>>(HarnessJson.Options))!.Data;

        Assert.Equal(Provider, payment!.ProviderEmail);
        Assert.Equal(Customer, payment.CustomerEmail);
    }

    [Fact]
    public async Task T205_AnAppointmentCannotBeChargedTwice()
    {
        using var service = await StartWithAnAppointmentAsync();

        var first = await service.Client.SendAsync(Authorised(
            HttpMethod.Post, $"api/v1/booking/appointments/{Appointment}/payment",
            Customer, TokenFactory.CustomerRole, new { amount = 50m, currency = "gbp" }));
        var second = await service.Client.SendAsync(Authorised(
            HttpMethod.Post, $"api/v1/booking/appointments/{Appointment}/payment",
            Customer, TokenFactory.CustomerRole, new { amount = 50m, currency = "gbp" }));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(1, await service.Database.GetCollection<PaymentEntity>("payments")
            .CountDocumentsAsync(Builders<PaymentEntity>.Filter.Empty));
    }

    [Fact]
    public async Task T205_AStrangerCanNeitherPayNorRead()
    {
        using var service = await StartWithAnAppointmentAsync();

        var pay = await service.Client.SendAsync(Authorised(
            HttpMethod.Post, $"api/v1/booking/appointments/{Appointment}/payment",
            Stranger, TokenFactory.CustomerRole, new { amount = 50m, currency = "gbp" }));
        var read = await service.Client.SendAsync(Authorised(
            HttpMethod.Get, $"api/v1/booking/appointments/{Appointment}/payment",
            Stranger, TokenFactory.CustomerRole));

        Assert.Equal(HttpStatusCode.Forbidden, pay.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
        Assert.Equal(0, await service.Database.GetCollection<PaymentEntity>("payments")
            .CountDocumentsAsync(Builders<PaymentEntity>.Filter.Empty));
    }

    [Fact]
    public async Task BothParticipantsCanReadThePayment()
    {
        using var service = await StartWithAnAppointmentAsync();

        await service.Client.SendAsync(Authorised(
            HttpMethod.Post, $"api/v1/booking/appointments/{Appointment}/payment",
            Customer, TokenFactory.CustomerRole, new { amount = 50m, currency = "gbp" }));

        foreach (var (subject, role) in new[] { (Provider, TokenFactory.ProviderRole), (Customer, TokenFactory.CustomerRole) })
        {
            var read = await service.Client.SendAsync(Authorised(
                HttpMethod.Get, $"api/v1/booking/appointments/{Appointment}/payment", subject, role));

            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public async Task ANonPositiveAmountIsRejected(decimal amount)
    {
        // ⚠️ This is the ONLY validation the amount gets, and it is not enough — see threat T-205(c). An
        // appointment does not record which service it was booked for, so there is no price to check against
        // and a customer can pay 0.01 for a 50 session. Accepted, documented, and it matters a great deal to
        // whoever first configures a real Stripe key.
        using var service = await StartWithAnAppointmentAsync();

        var response = await service.Client.SendAsync(Authorised(
            HttpMethod.Post, $"api/v1/booking/appointments/{Appointment}/payment",
            Customer, TokenFactory.CustomerRole, new { amount, currency = "gbp" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
