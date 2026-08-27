using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Booking.Requests;
using Library.Entities;
using MobileApp.Routing;
using MongoDB.Bson;

namespace AgendaBuddy.IntegrationTests.Harness;

/// <summary>
/// F-015-T07 AC2: every corrected <c>MobileApp.Routing.*RouteBuilder</c> method is exercised here by
/// building a real <see cref="HttpRequestMessage"/> from its <see cref="RouteSpec"/> output and firing it
/// at the real backend service that owns the route — not a hand-written path string duplicated in this
/// project's own tests. Proves "2xx or a correctly-typed error, not a 404 caused by a wrong path, verb, or
/// prefix" (the acceptance criterion's own wording) for the routes this task corrected or added.
/// </summary>
/// <remarks>
/// Authorization-boundary behaviour (who may call what) is already exhaustively covered by F-014's own
/// suite (<see cref="PaymentsAndStatusTest"/>, <see cref="SessionNotesTest"/>,
/// <see cref="ReportAndDeactivationTest"/>, <see cref="MessagingAndNotificationsTest"/>,
/// <see cref="CalendarOwnershipTest"/>, <see cref="CustomerListRoleTest"/>) — the classes below do not
/// repeat that; they prove the CLIENT'S route-building code, not just the backend route table, resolves.
/// </remarks>
internal static class MobileRouteRequests
{
    public static HttpRequestMessage Build(RouteSpec route, string token, object? body = null)
    {
        var request = new HttpRequestMessage(route.Method, route.Path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return request;
    }
}

// ── Booking ────────────────────────────────────────────────────────────────────────────────────────────

[Collection(HarnessCollection.Name)]
public class MobileBookingRouteResolutionTest(ServiceHostFixture<BookingAnchor> host, CryptoSessionFixture crypto)
    : IClassFixture<ServiceHostFixture<BookingAnchor>>
{
    private const string Provider = "route-check-provider@example.com";
    private const string Customer = "route-check-customer@example.com";
    private const string Appointment = "route-check-appointment";

    private readonly TokenFactory _tokens = new(crypto);

    private async Task<ServiceHost> SeedAsync()
    {
        var service = host.StartService("Production");
        await service.Database.GetCollection<AppointmentEntity>("appointments").InsertOneAsync(
            new AppointmentEntity
            {
                Id = ObjectId.GenerateNewId(),
                Identifier = Appointment,
                EmailProvider = Provider,
                EmailCustomer = Customer,
                Start = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc),
                End = new DateTime(2026, 9, 1, 11, 0, 0, DateTimeKind.Utc)
            });
        return service;
    }

    [Fact]
    public async Task UpdateAppointmentStatus_ResolvesAndTransitions()
    {
        using var service = await SeedAsync();
        var route = BookingRouteBuilder.UpdateAppointmentStatus(Appointment);
        var payload = BookingRouteBuilder.BuildUpdateStatusPayload(AppointmentStatus.Booked);

        var response = await service.Client.SendAsync(MobileRouteRequests.Build(
            route, _tokens.CreateToken(Customer, TokenFactory.CustomerRole), payload));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // F-019-T11 (AC8): live confirmation of the DataResponse<T> envelope, not just the status code.
        var wrapper = await response.Content.ReadFromJsonAsync<DataResponse<AppointmentStatusResponse>>(HarnessJson.Options);
        Assert.Equal(nameof(AppointmentStatus.Booked), wrapper!.Data!.Status);
    }

    [Fact]
    public async Task CreateNote_ResolvesAndCreates()
    {
        using var service = await SeedAsync();
        var route = BookingRouteBuilder.CreateNote(Appointment);
        var payload = BookingRouteBuilder.BuildNotePayload("Client mentioned a knee injury.");

        var response = await service.Client.SendAsync(MobileRouteRequests.Build(
            route, _tokens.CreateToken(Provider, TokenFactory.ProviderRole), payload));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task GetNotes_Resolves()
    {
        using var service = await SeedAsync();
        var route = BookingRouteBuilder.GetNotes(Appointment);

        var response = await service.Client.SendAsync(MobileRouteRequests.Build(
            route, _tokens.CreateToken(Provider, TokenFactory.ProviderRole)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateNote_Resolves()
    {
        using var service = await SeedAsync();
        var created = await service.Client.SendAsync(MobileRouteRequests.Build(
            BookingRouteBuilder.CreateNote(Appointment),
            _tokens.CreateToken(Provider, TokenFactory.ProviderRole),
            BookingRouteBuilder.BuildNotePayload("first draft")));
        // F-019-T06: DataResponse<T> envelope -- the identifier moved from the root to .data.
        var note = (await created.Content.ReadFromJsonAsync<DataResponse<NoteEntity>>(HarnessJson.Options))!.Data;

        var route = BookingRouteBuilder.UpdateNote(note!.Id.ToString());
        var response = await service.Client.SendAsync(MobileRouteRequests.Build(
            route, _tokens.CreateToken(Provider, TokenFactory.ProviderRole),
            BookingRouteBuilder.BuildNotePayload("corrected")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreatePayment_ResolvesAndCreates()
    {
        using var service = await SeedAsync();
        var route = BookingRouteBuilder.CreatePayment(Appointment);
        var payload = BookingRouteBuilder.BuildPaymentPayload(50m, "usd");

        var response = await service.Client.SendAsync(MobileRouteRequests.Build(
            route, _tokens.CreateToken(Customer, TokenFactory.CustomerRole), payload));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task GetPayment_Resolves()
    {
        using var service = await SeedAsync();
        await service.Client.SendAsync(MobileRouteRequests.Build(
            BookingRouteBuilder.CreatePayment(Appointment),
            _tokens.CreateToken(Customer, TokenFactory.CustomerRole),
            BookingRouteBuilder.BuildPaymentPayload(50m, "usd")));

        var route = BookingRouteBuilder.GetPayment(Appointment);
        var response = await service.Client.SendAsync(MobileRouteRequests.Build(
            route, _tokens.CreateToken(Provider, TokenFactory.ProviderRole)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // F-019-T11 (AC8): live confirmation of the DataResponse<T> envelope, not just the status code.
        var wrapper = await response.Content.ReadFromJsonAsync<DataResponse<PaymentEntity>>(HarnessJson.Options);
        Assert.Equal(50m, wrapper!.Data!.Amount);
    }
}

// ── Calendar ───────────────────────────────────────────────────────────────────────────────────────────

[Collection(HarnessCollection.Name)]
public class MobileCalendarRouteResolutionTest(ServiceHostFixture<CalendarAnchor> host, CryptoSessionFixture crypto)
    : IClassFixture<ServiceHostFixture<CalendarAnchor>>
{
    private const string Provider = "route-check-calendar-provider@example.com";

    private readonly TokenFactory _tokens = new(crypto);

    private async Task<ServiceHost> SeedAsync()
    {
        var service = host.StartService("Production");
        await service.Database.GetCollection<ProviderEntity>("providers").InsertOneAsync(new ProviderEntity
        {
            Id = ObjectId.GenerateNewId(),
            FirstName = "Route",
            LastName = "Check",
            Email = Provider,
            AppointmentEntities = []
        });
        return service;
    }

    // F-015-T07 deviation: this is also the real read path BookingApiService.GetTodayAppointmentsAsync /
    // GetAppointmentAsync compose with — Booking itself has no GET route (see BookingRouteBuilder).
    [Fact]
    public async Task Appointments_Resolves()
    {
        using var service = await SeedAsync();
        var route = CalendarRouteBuilder.Appointments(Provider);

        var response = await service.Client.SendAsync(MobileRouteRequests.Build(
            route, _tokens.CreateToken(Provider, TokenFactory.ProviderRole)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Availability_Resolves()
    {
        using var service = await SeedAsync();
        var route = CalendarRouteBuilder.Availability(Provider, DateOnly.FromDateTime(DateTime.UtcNow), 30);

        var response = await service.Client.SendAsync(MobileRouteRequests.Build(
            route, _tokens.CreateToken(Provider, TokenFactory.ProviderRole)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

// ── Customer / Messaging / Notifications (all hosted by the Customer service) ────────────────────────

[Collection(HarnessCollection.Name)]
public class MobileCustomerMessagingRouteResolutionTest(
    ServiceHostFixture<CustomerAnchor> host, CryptoSessionFixture crypto)
    : IClassFixture<ServiceHostFixture<CustomerAnchor>>
{
    private const string Caller = "route-check-caller@example.com";
    private const string Counterpart = "route-check-counterpart@example.com";

    private readonly TokenFactory _tokens = new(crypto);

    [Fact]
    public async Task Customers_ResolvesForAProviderCaller()
    {
        using var service = host.StartService("Production");
        var route = CustomerRouteBuilder.Customers();

        var response = await service.Client.SendAsync(MobileRouteRequests.Build(
            route, _tokens.CreateToken(Caller, TokenFactory.ProviderRole)));

        // Not the point of this test whether the list is empty — only that the corrected prefix resolves
        // against a real route, not a 404.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SendMessage_InboxThreadAndMarkRead_AllResolve()
    {
        using var service = host.StartService("Production");

        var sent = await service.Client.SendAsync(MobileRouteRequests.Build(
            MessagingRouteBuilder.SendMessage(),
            _tokens.CreateToken(Caller, TokenFactory.CustomerRole),
            MessagingRouteBuilder.BuildSendMessagePayload(Counterpart, "Can we move to 4pm?")));
        Assert.Equal(HttpStatusCode.Created, sent.StatusCode);
        var message = await sent.Content.ReadFromJsonAsync<MessageEntity>(HarnessJson.Options);

        var inbox = await service.Client.SendAsync(MobileRouteRequests.Build(
            MessagingRouteBuilder.Inbox(), _tokens.CreateToken(Counterpart, TokenFactory.CustomerRole)));
        Assert.Equal(HttpStatusCode.OK, inbox.StatusCode);

        var thread = await service.Client.SendAsync(MobileRouteRequests.Build(
            MessagingRouteBuilder.Thread(Counterpart), _tokens.CreateToken(Caller, TokenFactory.CustomerRole)));
        Assert.Equal(HttpStatusCode.OK, thread.StatusCode);

        // POST, not the client's former PATCH — the real route only accepts POST.
        var markRead = await service.Client.SendAsync(MobileRouteRequests.Build(
            MessagingRouteBuilder.MarkRead(message!.Id.ToString()),
            _tokens.CreateToken(Counterpart, TokenFactory.CustomerRole)));
        Assert.Equal(HttpStatusCode.NoContent, markRead.StatusCode);
    }

    [Fact]
    public async Task Notifications_AndMarkRead_BothResolve()
    {
        using var service = host.StartService("Production");
        var notification = new NotificationEntity
        {
            Id = ObjectId.GenerateNewId(),
            RecipientEmail = Caller,
            Subject = "Appointment confirmed",
            Body = "Thursday 4pm"
        };
        await service.Database.GetCollection<NotificationEntity>("notifications").InsertOneAsync(notification);

        var list = await service.Client.SendAsync(MobileRouteRequests.Build(
            NotificationRouteBuilder.Notifications(), _tokens.CreateToken(Caller, TokenFactory.CustomerRole)));
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        // POST, not the client's former PATCH.
        var markRead = await service.Client.SendAsync(MobileRouteRequests.Build(
            NotificationRouteBuilder.MarkRead(notification.Id.ToString()),
            _tokens.CreateToken(Caller, TokenFactory.CustomerRole)));
        Assert.Equal(HttpStatusCode.NoContent, markRead.StatusCode);
    }
}

// ── Provider ───────────────────────────────────────────────────────────────────────────────────────────

[Collection(HarnessCollection.Name)]
public class MobileProviderRouteResolutionTest(ServiceHostFixture<ProviderAnchor> host, CryptoSessionFixture crypto)
    : IClassFixture<ServiceHostFixture<ProviderAnchor>>
{
    private const string Provider = "route-check-report-provider@example.com";

    private readonly TokenFactory _tokens = new(crypto);

    private async Task<ServiceHost> SeedAsync()
    {
        var service = host.StartService("Production");
        await service.Database.GetCollection<ProviderEntity>("providers").InsertOneAsync(new ProviderEntity
        {
            Id = ObjectId.GenerateNewId(),
            FirstName = "Route",
            LastName = "Check",
            Email = Provider
        });
        return service;
    }

    [Fact]
    public async Task Report_Resolves()
    {
        using var service = await SeedAsync();
        var route = ProviderRouteBuilder.Report(Provider);

        var response = await service.Client.SendAsync(MobileRouteRequests.Build(
            route, _tokens.CreateToken(Provider, TokenFactory.ProviderRole)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Deactivate_Resolves()
    {
        using var service = await SeedAsync();
        var route = ProviderRouteBuilder.Deactivate(Provider);

        var response = await service.Client.SendAsync(MobileRouteRequests.Build(
            route, _tokens.CreateToken(Provider, TokenFactory.ProviderRole)));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }
}
