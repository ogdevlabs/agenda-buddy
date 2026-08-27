using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AgendaBuddy.EventAndCommands.Persistence;
using AgendaBuddy.Library.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AgendaBuddy.IntegrationTests.Harness;

/// <summary>
/// A provider reads their own report and deactivates
/// themselves, and nobody else can do either.
/// </summary>
[Collection(HarnessCollection.Name)]
public class ReportAndDeactivationTest(ServiceHostFixture<ProviderAnchor> host, CryptoSessionFixture crypto)
    : IClassFixture<ServiceHostFixture<ProviderAnchor>>
{
    private const string Owner = "report-owner@example.com";
    private const string OtherProvider = "other-report-provider@example.com";
    private const string TheCustomer = "report-customer@example.com";

    private readonly TokenFactory _tokens = new(crypto);

    private async Task<ServiceHost> StartWithAProviderAsync()
    {
        var service = host.StartService("Production");

        await service.Database.GetCollection<ProviderEntity>("providers").InsertManyAsync(
        [
            new ProviderEntity
            {
                Id = ObjectId.GenerateNewId(), FirstName = "Report", LastName = "Owner", Email = Owner,
                ServiceEntities =
                [
                    new ServiceEntity("Coaching", "1hr", 50m) { IsActive = true },
                    new ServiceEntity("Assessment", "30m", 80m) { IsActive = true }
                ],
                AppointmentEntities =
                [
                    Appointment(AppointmentStatus.Completed, TheCustomer),
                    Appointment(AppointmentStatus.Completed, TheCustomer),
                    Appointment(AppointmentStatus.Booked, "someone-else@example.com"),
                    Appointment(AppointmentStatus.Requested, "third@example.com")
                ]
            },
            new ProviderEntity
            {
                Id = ObjectId.GenerateNewId(), FirstName = "Other", LastName = "Provider",
                Email = OtherProvider
            }
        ]);

        return service;
    }

    private static AppointmentEntity Appointment(AppointmentStatus status, string customer) => new()
    {
        Id = ObjectId.GenerateNewId(),
        EmailProvider = Owner,
        EmailCustomer = customer,
        AppointmentStatus = status,
        Start = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc),
        End = new DateTime(2026, 9, 1, 11, 0, 0, DateTimeKind.Utc)
    };

    private HttpRequestMessage Authorised(HttpMethod method, string path, string subject, string role)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokens.CreateToken(subject, role));
        return request;
    }

    [Theory]
    [InlineData("GET", "api/v1/providers/report-owner@example.com/report")]
    [InlineData("POST", "api/v1/providers/report-owner@example.com/deactivate")]
    public async Task AC8_BothNewProviderRoutes_RefuseAnAnonymousCaller(string method, string path)
    {
        using var service = await StartWithAProviderAsync();

        var response = await service.Client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AC4_AProviderReadsTheirOwnCounts()
    {
        using var service = await StartWithAProviderAsync();

        var response = await service.Client.SendAsync(Authorised(
            HttpMethod.Get, $"api/v1/providers/{Owner}/report", Owner, TokenFactory.ProviderRole));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var report = await response.Content.ReadFromJsonAsync<ProviderReport>(HarnessJson.Options);

        Assert.Equal(4, report!.TotalBookings);

        // ⚠️ These two numbers were STRUCTURALLY ZERO before this fix: nothing in production ever set a status
        // other than Requested, because Book()/Complete() were never called and the update path copied whatever
        // the client sent. Wiring reporting without fixing that would have shipped a dashboard permanently
        // reporting no completed work — which is worse than an unreachable endpoint, because it looks like a
        // fact rather than a bug.
        Assert.Equal(2, report.CompletedAppointments);
        Assert.Equal(3, report.UniqueCustomers);
        Assert.Equal(0, report.CancelledAppointments);
    }

    [Fact]
    public async Task AC18_TheReportPublishesNoRevenueFigure_AndSaysWhy()
    {
        using var service = await StartWithAProviderAsync();

        var response = await service.Client.SendAsync(Authorised(
            HttpMethod.Get, $"api/v1/providers/{Owner}/report", Owner, TokenFactory.ProviderRole));

        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body).RootElement;

        // No field a client could bind a number to. The old formula was completed × the WHOLE service
        // catalogue's fees — this provider has two services at 50 and 80 and two completed appointments, so it
        // would have reported 260 with the confidence of a bank statement.
        Assert.False(json.TryGetProperty("estimatedRevenue", out _),
            $"the report still carries a revenue figure: {body}");

        Assert.False(json.GetProperty("revenueAvailable").GetBoolean());

        // And the absence is EXPLAINED rather than silent, so a client renders a reason instead of £0.
        var reason = json.GetProperty("revenueUnavailableReason").GetString();
        Assert.Contains("service", reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AC9_AProviderCannotReadAnotherProvidersReport()
    {
        using var service = await StartWithAProviderAsync();

        var response = await service.Client.SendAsync(Authorised(
            HttpMethod.Get, $"api/v1/providers/{Owner}/report", OtherProvider, TokenFactory.ProviderRole));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AC10_ACustomerCannotReadAReport()
    {
        using var service = await StartWithAProviderAsync();

        // Even the customer whose appointments the numbers count. Revenue and retention are the provider's
        // business, not shared data.
        var response = await service.Client.SendAsync(Authorised(
            HttpMethod.Get, $"api/v1/providers/{Owner}/report", TheCustomer, TokenFactory.CustomerRole));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AC5_AProviderDeactivatesThemselves_AndTheCommandIsAudited()
    {
        using var service = await StartWithAProviderAsync();

        var response = await service.Client.SendAsync(Authorised(
            HttpMethod.Post, $"api/v1/providers/{Owner}/deactivate", Owner, TokenFactory.ProviderRole));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var stored = await service.Database.GetCollection<ProviderEntity>("providers")
            .Find(Builders<ProviderEntity>.Filter.Eq(p => p.Email, Owner)).SingleAsync();
        Assert.False(stored.IsActive);

        // The appointments survive the deactivation — proof that the targeted $set replaced a whole-document
        // write rather than merely moving it (requirement 20).
        Assert.Equal(4, stored.AppointmentEntities.Count);

        // The command handler had never been dispatched by anything before this route existed, so this is the first time its
        // audit event has ever been written. Read back from the events collection, which no unit test can do.
        var events = await service.Database.GetCollection<Event>("events")
            .Find(Builders<Event>.Filter.Eq(e => e.Type, "DeactivateProviderCommand")).ToListAsync();

        var audit = Assert.Single(events);
        Assert.Equal("Success", audit.Status);
    }

    [Fact]
    public async Task T207_AProviderCannotDeactivateSomebodyElse()
    {
        using var service = await StartWithAProviderAsync();

        var response = await service.Client.SendAsync(Authorised(
            HttpMethod.Post, $"api/v1/providers/{Owner}/deactivate", OtherProvider, TokenFactory.ProviderRole));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var stored = await service.Database.GetCollection<ProviderEntity>("providers")
            .Find(Builders<ProviderEntity>.Filter.Eq(p => p.Email, Owner)).SingleAsync();
        Assert.True(stored.IsActive);

        Assert.Equal(0, await service.Database.GetCollection<Event>("events")
            .CountDocumentsAsync(Builders<Event>.Filter.Eq(e => e.Type, "DeactivateProviderCommand")));
    }

    [Fact]
    public async Task T207_ACustomerCannotDeactivateAProvider()
    {
        // There is no administrative role in this product — Identity's allow-list is exactly
        // {Provider, Customer} (ADR-025) — so a provider deactivating themselves is the only legitimate call,
        // and an unguarded version would let anyone take a business offline.
        using var service = await StartWithAProviderAsync();

        var response = await service.Client.SendAsync(Authorised(
            HttpMethod.Post, $"api/v1/providers/{TheCustomer}/deactivate", TheCustomer, TokenFactory.CustomerRole));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeactivatingAProviderThatDoesNotExist_Answers404()
    {
        using var service = await StartWithAProviderAsync();

        var response = await service.Client.SendAsync(Authorised(
            HttpMethod.Post, "api/v1/providers/ghost@example.com/deactivate",
            "ghost@example.com", TokenFactory.ProviderRole));

        // Safe: the path email had to equal the caller's own claim to get this far, so this can only mean the
        // caller has no provider record.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
