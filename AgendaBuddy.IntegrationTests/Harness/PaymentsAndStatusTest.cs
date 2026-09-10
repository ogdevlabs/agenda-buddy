using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AgendaBuddy.Library.Entities;
using AgendaBuddy.Library.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AgendaBuddy.IntegrationTests.Harness;

/// <summary>
/// Appointment status is server-owned and drives the payment authorization, capture, and release lifecycle.
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
        AppointmentStatus status = AppointmentStatus.Requested,
        DateTime? startUtc = null)
    {
        var service = host.StartService("Production", new Dictionary<string, string>
        {
            ["Security:Local"] = "true"
        });

        // The default is a fixed past date, which suits every test about the cancel/status MECHANISM. The
        // customer's notice period is the one rule that cannot be expressed against a fixed date -- "more than
        // 24 hours away" is relative to now -- so that test supplies its own.
        var start = startUtc ?? new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);

        var appointment = new AppointmentEntity
        {
            Id = ObjectId.GenerateNewId(),
            Identifier = Appointment,
            EmailProvider = Provider,
            EmailCustomer = Customer,
            Start = start,
            End = start.AddHours(1),
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
        // The forgery this guards against: UpdateAppointmentCommandHandler used to copy whatever
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

        // Live confirmation the response
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

        // The provider confirms, not the customer: every transition on this route is provider-only, so a
        // customer self-confirming their own request is refused (see BookingModule's ownership guards).
        var booked = await service.Client.SendAsync(Authorised(
            HttpMethod.Post, $"api/v1/booking/appointments/{Appointment}/status",
            Provider, TokenFactory.ProviderRole, new { status = "Booked" }));

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

    // Cancelled is NOT in this list. It is a legal transition target now that cancellation is a soft delete --
    // but only through DELETE /appointments/, not through this status route, which is covered below.
    [Theory]
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

        // As the PROVIDER, who may cancel at any notice: this asserts the soft delete and the embedded copy,
        // not the customer's notice period, and the fixture's window is a fixed past date.
        var cancelBooked = await booked.Client.SendAsync(Authorised(
            HttpMethod.Delete, "api/v1/booking/appointments/", Provider, TokenFactory.ProviderRole,
            new
            {
                identifier = Appointment,
                emailProvider = Provider,
                emailCustomer = Customer,
                start = "2026-09-01T10:00:00Z",
                end = "2026-09-01T11:00:00Z"
            }));

        Assert.Equal(HttpStatusCode.NoContent, cancelBooked.StatusCode);

        // A SOFT delete: the document survives, carrying Cancelled. It used to be removed outright, which left
        // no record that the slot had ever been booked -- so reporting could not count a cancellation and a
        // cancellation notification named an appointment nothing could fetch.
        var storedAfterCancel = await booked.Database.GetCollection<AppointmentEntity>("appointments")
            .Find(Builders<AppointmentEntity>.Filter.Eq(a => a.Identifier, Appointment)).SingleAsync();
        Assert.Equal(AppointmentStatus.Cancelled, storedAfterCancel.AppointmentStatus);

        // The provider's embedded copy is updated in place, not removed -- ReportingService counts from there,
        // so the two stores disagreeing would make the dashboard report the old status indefinitely.
        var providerAfterCancel = await booked.Database.GetCollection<ProviderEntity>("providers")
            .Find(Builders<ProviderEntity>.Filter.Eq(p => p.Email, Provider)).SingleAsync();
        var embedded = Assert.Single(
            providerAfterCancel.AppointmentEntities.Where(a => a.Identifier == Appointment));
        Assert.Equal(AppointmentStatus.Cancelled, embedded.AppointmentStatus);

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

        // Untouched, and still Completed: "cancel" is not a thing you can do to work already delivered. The
        // rule is in the update's own filter, so a caller that read Booked before it completed cannot win a
        // race against it.
        var storedCompleted = await completed.Database.GetCollection<AppointmentEntity>("appointments")
            .Find(Builders<AppointmentEntity>.Filter.Eq(a => a.Identifier, Appointment)).SingleAsync();
        Assert.Equal(AppointmentStatus.Completed, storedCompleted.AppointmentStatus);
    }

    /// <summary>
    /// A cancelled appointment frees its slot. This is the half of the soft delete that would be easiest to get
    /// wrong: keeping the record and still treating it as busy would permanently shrink a provider's bookable
    /// calendar every time anyone called a session off.
    /// </summary>
    [Fact]
    public async Task ACancelledAppointmentNoLongerBlocksItsSlot()
    {
        using var service = await StartWithAnAppointmentAsync(AppointmentStatus.Booked);

        // As the PROVIDER: this test is about the slot being freed, and a provider may cancel at any notice.
        // The fixture's window is a fixed past date, so a CUSTOMER is refused by the 24-hour rule -- see
        // ACustomerCannotCancelInsideTheNoticePeriod for that half.
        var cancel = await service.Client.SendAsync(Authorised(
            HttpMethod.Delete, "api/v1/booking/appointments/", Provider, TokenFactory.ProviderRole,
            new
            {
                identifier = Appointment,
                emailProvider = Provider,
                emailCustomer = Customer,
                start = "2026-09-01T10:00:00Z",
                end = "2026-09-01T11:00:00Z"
            }));
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);

        // Rebooking the same window must now succeed. Before the status clause was added to the overlap filter,
        // this answered 409: the slot looked free on the calendar and every attempt to take it was refused.
        var rebook = await service.Client.SendAsync(Authorised(
            HttpMethod.Post, "api/v1/booking/appointments", Customer, TokenFactory.CustomerRole,
            new
            {
                identifier = Guid.NewGuid().ToString(),
                emailProvider = Provider,
                emailCustomer = Customer,
                start = "2026-09-01T10:00:00Z",
                end = "2026-09-01T11:00:00Z",
                dayOff = false
            }));

        Assert.NotEqual(HttpStatusCode.Conflict, rebook.StatusCode);
    }

    /// <summary>
    /// A CUSTOMER must give 24 hours' notice; inside that window the cancellation is refused and the refusal
    /// names the deadline.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The rule rides in the same atomic filter as the cancellable-status rule, so this also proves the
    /// appointment is left untouched — a refusal that had already written would be worse than one that had not.
    /// </para>
    /// <para>
    /// The wording is asserted because it is the whole point: only the server holds the authoritative clock, so
    /// the deadline cannot be reconstructed on the client, and "could not cancel" is advice nobody can act on.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task ACustomerCannotCancelInsideTheNoticePeriod()
    {
        using var service = await StartWithAnAppointmentAsync(
            AppointmentStatus.Booked, DateTime.UtcNow.AddHours(6));

        var cancel = await service.Client.SendAsync(Authorised(
            HttpMethod.Delete, "api/v1/booking/appointments/", Customer, TokenFactory.CustomerRole,
            new { identifier = Appointment, emailProvider = Provider, emailCustomer = Customer }));

        Assert.Equal(HttpStatusCode.BadRequest, cancel.StatusCode);
        Assert.Contains("Cancellations close 24 hours", await cancel.Content.ReadAsStringAsync());

        // Refused, not partly applied.
        var stored = await service.Database.GetCollection<AppointmentEntity>("appointments")
            .Find(Builders<AppointmentEntity>.Filter.Eq(a => a.Identifier, Appointment)).SingleAsync();
        Assert.Equal(AppointmentStatus.Booked, stored.AppointmentStatus);
    }

    [Fact]
    public async Task ACustomerCanCancelOutsideTheNoticePeriod()
    {
        using var service = await StartWithAnAppointmentAsync(
            AppointmentStatus.Booked, DateTime.UtcNow.AddDays(5));

        var cancel = await service.Client.SendAsync(Authorised(
            HttpMethod.Delete, "api/v1/booking/appointments/", Customer, TokenFactory.CustomerRole,
            new { identifier = Appointment, emailProvider = Provider, emailCustomer = Customer }));

        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
    }

    /// <summary>
    /// A PROVIDER may cancel at any notice. The asymmetry is deliberate: a session they cannot make is
    /// unavoidable, while a customer cancelling an hour beforehand costs a slot nobody else can now take.
    /// </summary>
    [Fact]
    public async Task AProviderCanCancelInsideTheNoticePeriod()
    {
        using var service = await StartWithAnAppointmentAsync(
            AppointmentStatus.Booked, DateTime.UtcNow.AddHours(6));

        var cancel = await service.Client.SendAsync(Authorised(
            HttpMethod.Delete, "api/v1/booking/appointments/", Provider, TokenFactory.ProviderRole,
            new { identifier = Appointment, emailProvider = Provider, emailCustomer = Customer }));

        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
    }

    // ── Payments ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ClientSuppliedPaymentRoute_IsRemoved()
    {
        using var service = await StartWithAnAppointmentAsync();

        var response = await service.Client.SendAsync(Authorised(
            HttpMethod.Post, $"api/v1/booking/appointments/{Appointment}/payment",
            Customer, TokenFactory.CustomerRole, new { amount = 1m, currency = "usd" }));

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Equal(0, await service.Database.GetCollection<PaymentEntity>("payments")
            .CountDocumentsAsync(Builders<PaymentEntity>.Filter.Empty));
    }

    [Fact]
    public async Task CustomerSetup_PersistsOnlyTokenizedMaskedPaymentData()
    {
        using var service = await StartWithAnAppointmentAsync();
        await service.Database.GetCollection<CustomerEntity>("customers").InsertOneAsync(new CustomerEntity
        {
            Id = ObjectId.GenerateNewId(),
            Email = Customer,
            FirstName = "Status",
            LastName = "Customer"
        });

        var response = await service.Client.SendAsync(Authorised(
            HttpMethod.Post, "api/v1/booking/payments/customer/setup",
            Customer, TokenFactory.CustomerRole));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = await service.Database.GetCollection<CustomerEntity>("customers")
            .Find(Builders<CustomerEntity>.Filter.Eq(customer => customer.Email, Customer)).SingleAsync();
        Assert.StartsWith("cus_local_", stored.StripeCustomerId);
        Assert.StartsWith("pm_local_", stored.StripeDefaultPaymentMethodId);
        Assert.Equal("Visa", stored.PaymentMethodBrand);
        Assert.Equal("4242", stored.PaymentMethodLast4);
        Assert.DoesNotContain(stored.ToBsonDocument().Names,
            name => name.Contains("number", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("cvc", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ProviderOnboarding_PersistsConnectedAccountReadiness()
    {
        using var service = await StartWithAnAppointmentAsync();

        var response = await service.Client.SendAsync(Authorised(
            HttpMethod.Post, "api/v1/booking/payments/provider/onboarding",
            Provider, TokenFactory.ProviderRole));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = await service.Database.GetCollection<ProviderEntity>("providers")
            .Find(Builders<ProviderEntity>.Filter.Eq(provider => provider.Email, Provider)).SingleAsync();
        Assert.StartsWith("acct_local_", stored.StripeConnectedAccountId);
        Assert.True(stored.StripeChargesEnabled);
        Assert.True(stored.StripePayoutsEnabled);
    }

    [Fact]
    public async Task ProviderConfirmation_AuthorizesTenPercentFee_ThenCompletionCaptures()
    {
        using var service = await StartWithAnAppointmentAsync();
        var appointments = service.Database.GetCollection<AppointmentEntity>("appointments");
        await appointments.UpdateOneAsync(
            Builders<AppointmentEntity>.Filter.Eq(appointment => appointment.Identifier, Appointment),
            Builders<AppointmentEntity>.Update
                .Set(appointment => appointment.PaymentAmountMinor, 10001)
                .Set(appointment => appointment.PaymentCurrency, "usd"));
        await service.Database.GetCollection<CustomerEntity>("customers").InsertOneAsync(new CustomerEntity
        {
            Id = ObjectId.GenerateNewId(),
            Email = Customer,
            StripeCustomerId = "cus_local_customer",
            StripeDefaultPaymentMethodId = "pm_local_card"
        });
        await service.Database.GetCollection<ProviderEntity>("providers").UpdateOneAsync(
            Builders<ProviderEntity>.Filter.Eq(provider => provider.Email, Provider),
            Builders<ProviderEntity>.Update
                .Set(provider => provider.StripeConnectedAccountId, "acct_local_provider")
                .Set(provider => provider.StripeChargesEnabled, true)
                .Set(provider => provider.StripePayoutsEnabled, true));

        var booked = await service.Client.SendAsync(Authorised(
            HttpMethod.Post, $"api/v1/booking/appointments/{Appointment}/status",
            Provider, TokenFactory.ProviderRole, new { status = "Booked" }));

        Assert.Equal(HttpStatusCode.OK, booked.StatusCode);
        var payment = await service.Database.GetCollection<PaymentEntity>("payments")
            .Find(Builders<PaymentEntity>.Filter.Eq(value => value.AppointmentIdentifier, Appointment)).SingleAsync();
        Assert.Equal(PaymentStatus.Authorized, payment.Status);
        Assert.Equal(1000, payment.ApplicationFeeMinor);
        Assert.Equal(9001, payment.ProviderAmountMinor);
        Assert.StartsWith(RecordingPaymentGateway.LocalIntentPrefix, payment.StripePaymentIntentId);

        var completed = await service.Client.SendAsync(Authorised(
            HttpMethod.Post, $"api/v1/booking/appointments/{Appointment}/status",
            Provider, TokenFactory.ProviderRole, new { status = "Completed" }));

        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        payment = await service.Database.GetCollection<PaymentEntity>("payments")
            .Find(Builders<PaymentEntity>.Filter.Eq(value => value.AppointmentIdentifier, Appointment)).SingleAsync();
        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        Assert.NotNull(payment.CapturedAt);
    }

    [Fact]
    public async Task ProviderCancellation_ReleasesTheWholeAuthorizationBeforeCancelling()
    {
        using var service = await StartWithAnAppointmentAsync(AppointmentStatus.Booked);
        await service.Database.GetCollection<PaymentEntity>("payments").InsertOneAsync(new PaymentEntity(
            Appointment, Provider, Customer, 100m)
        {
            Id = ObjectId.GenerateNewId(),
            AmountMinor = 10000,
            ProviderAmountMinor = 9000,
            ApplicationFeeMinor = 1000,
            StripePaymentIntentId = "local_authorized",
            Status = PaymentStatus.Authorized
        });

        var response = await service.Client.SendAsync(Authorised(
            HttpMethod.Delete, "api/v1/booking/appointments/", Provider, TokenFactory.ProviderRole,
            new { identifier = Appointment, emailProvider = Provider, emailCustomer = Customer }));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var payment = await service.Database.GetCollection<PaymentEntity>("payments")
            .Find(Builders<PaymentEntity>.Filter.Eq(value => value.AppointmentIdentifier, Appointment)).SingleAsync();
        Assert.Equal(PaymentStatus.Cancelled, payment.Status);
        Assert.Equal(AppointmentStatus.Cancelled, (await StoredAsync(service)).AppointmentStatus);
    }
}
