using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AgendaBuddy.Library.Calendar;
using AgendaBuddy.Library.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AgendaBuddy.IntegrationTests.Harness;

/// <summary>
/// The subscribed calendar feed end to end (ADR-073/074): the owner manages it with a bearer token, a calendar app
/// fetches it with nothing but the URL, and every way a URL stops working looks the same from outside.
/// </summary>
[Collection(HarnessCollection.Name)]
public class CalendarFeedTest : IClassFixture<ServiceHostFixture<CalendarAnchor>>, IClassFixture<ServiceHostFixture<CustomerAnchor>>
{
    private const string Provider = "feed-coach@example.com";
    private const string Customer = "feed-client@example.com";

    private readonly ServiceHostFixture<CalendarAnchor> _calendarHost;
    private readonly ServiceHostFixture<CustomerAnchor> _customerHost;
    private readonly TokenFactory _tokens;

    public CalendarFeedTest(
        ServiceHostFixture<CalendarAnchor> calendarHost,
        ServiceHostFixture<CustomerAnchor> customerHost,
        CryptoSessionFixture crypto)
    {
        _calendarHost = calendarHost;
        _customerHost = customerHost;
        _tokens = new TokenFactory(crypto);
    }

    private async Task<ServiceHost> StartWithABookedSession()
    {
        var service = _calendarHost.StartService("Production");
        var start = DateTime.UtcNow.Date.AddDays(3).AddHours(15);

        await service.Database.GetCollection<ProviderEntity>("providers").InsertOneAsync(new ProviderEntity
        {
            Id = ObjectId.GenerateNewId(),
            FirstName = "Ana",
            LastName = "Coach",
            Email = Provider,
            AppointmentEntities =
            [
                new AppointmentEntity
                {
                    Id = ObjectId.GenerateNewId(),
                    Identifier = "feed-appt-1",
                    EmailProvider = Provider,
                    EmailCustomer = Customer,
                    Start = start,
                    End = start.AddHours(1),
                    AppointmentStatus = AppointmentStatus.Booked,
                    ServiceName = "Yoga"
                },
            ],
        });
        await service.Database.GetCollection<CustomerEntity>("customers").InsertOneAsync(new CustomerEntity
        {
            Id = ObjectId.GenerateNewId(),
            FirstName = "Luis",
            LastName = "Client",
            Email = Customer
        });

        return service;
    }

    private HttpRequestMessage AsOwner(HttpMethod method, string caller, string role, object? body = null)
    {
        var request = new HttpRequestMessage(method, "api/v1/calendar/feed");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokens.CreateToken(caller, role));
        if (body is not null) request.Content = JsonContent.Create(body);
        return request;
    }

    private async Task<string> EnableAsync(ServiceHost service, string caller, string role, string? language = null)
    {
        var response = await service.Client.SendAsync(
            AsOwner(HttpMethod.Post, caller, role, new { language }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var url = json.RootElement.GetProperty("data").GetProperty("url").GetString()!;
        return new Uri(url).AbsolutePath.TrimStart('/');
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    [InlineData("DELETE")]
    public async Task ManagingTheFeedRequiresAToken(string method)
    {
        using var service = _calendarHost.StartService("Production");

        var response = await service.Client.SendAsync(new HttpRequestMessage(new HttpMethod(method), "api/v1/calendar/feed"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AnEnabledFeedServesTheCalendarAnonymously()
    {
        using var service = await StartWithABookedSession();
        var path = await EnableAsync(service, Provider, TokenFactory.ProviderRole);

        var response = await service.Client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/calendar", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        var ics = await response.Content.ReadAsStringAsync();
        Assert.StartsWith("BEGIN:VCALENDAR\r\n", ics);
        Assert.Contains("SUMMARY:Yoga with Luis Client\r\n", ics);
        Assert.Contains("UID:feed-appt-1@agendame\r\n", ics);
    }

    [Fact]
    public async Task TheFeedNeverCarriesAnEmailAddress()
    {
        using var service = await StartWithABookedSession();
        var path = await EnableAsync(service, Provider, TokenFactory.ProviderRole);

        var ics = await service.Client.GetStringAsync(path);

        Assert.DoesNotContain("@example.com", ics, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ACustomerFeedIsGatheredFromTheProvidersBook()
    {
        using var service = await StartWithABookedSession();
        var path = await EnableAsync(service, Customer, TokenFactory.CustomerRole, "es-MX");

        var ics = await service.Client.GetStringAsync(path);

        Assert.Contains("SUMMARY:Yoga con Ana Coach\r\n", ics);
    }

    [Fact]
    public async Task StatusReportsTheFeedButNeverItsUrl()
    {
        using var service = await StartWithABookedSession();
        var path = await EnableAsync(service, Provider, TokenFactory.ProviderRole);

        var response = await service.Client.SendAsync(AsOwner(HttpMethod.Get, Provider, TokenFactory.ProviderRole));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"enabled\":true", body);
        Assert.DoesNotContain(path.Split('/').Last().Replace(".ics", ""), body);
    }

    [Fact]
    public async Task ResettingRevokesThePreviousUrl()
    {
        using var service = await StartWithABookedSession();
        var first = await EnableAsync(service, Provider, TokenFactory.ProviderRole);
        var second = await EnableAsync(service, Provider, TokenFactory.ProviderRole);

        Assert.NotEqual(first, second);
        Assert.Equal(HttpStatusCode.NotFound, (await service.Client.GetAsync(first)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await service.Client.GetAsync(second)).StatusCode);
    }

    [Fact]
    public async Task TurningOffRevokesTheUrlAndIsIdempotent()
    {
        using var service = await StartWithABookedSession();
        var path = await EnableAsync(service, Provider, TokenFactory.ProviderRole);

        var first = await service.Client.SendAsync(AsOwner(HttpMethod.Delete, Provider, TokenFactory.ProviderRole));
        var again = await service.Client.SendAsync(AsOwner(HttpMethod.Delete, Provider, TokenFactory.ProviderRole));

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await service.Client.GetAsync(path)).StatusCode);
    }

    /// <summary>Unknown, malformed and revoked tokens must be indistinguishable, or the route is a token oracle.</summary>
    [Fact]
    public async Task EveryFailureAnswersTheSame404()
    {
        using var service = await StartWithABookedSession();
        var revoked = await EnableAsync(service, Provider, TokenFactory.ProviderRole);
        await service.Client.SendAsync(AsOwner(HttpMethod.Delete, Provider, TokenFactory.ProviderRole));

        var probes = new[]
        {
            revoked,
            $"api/v1/calendar/feed/{CalendarFeedToken.New()}.ics",
            "api/v1/calendar/feed/not-a-token.ics",
            $"api/v1/calendar/feed/{CalendarFeedToken.New()}"
        };

        var answers = new List<(HttpStatusCode Status, string Body)>();
        foreach (var probe in probes)
        {
            var response = await service.Client.GetAsync(probe);
            answers.Add((response.StatusCode, await response.Content.ReadAsStringAsync()));
        }

        Assert.All(answers, answer => Assert.Equal(HttpStatusCode.NotFound, answer.Status));
        Assert.Single(answers.Select(answer => answer.Body).Distinct());
    }

    [Fact]
    public async Task OnlyTheHashOfTheTokenIsStored()
    {
        using var service = await StartWithABookedSession();
        var path = await EnableAsync(service, Provider, TokenFactory.ProviderRole);
        var token = path.Split('/').Last()[..^4];

        var stored = await service.Database.GetCollection<BsonDocument>("calendar_feeds")
            .Find(new BsonDocument("owner_email", Provider)).SingleAsync();

        Assert.DoesNotContain(token, stored.ToJson());
        Assert.Equal(CalendarFeedToken.Hash(token), stored["token_hash"].AsString);
    }

    [Fact]
    public async Task DeletingTheAccountRevokesTheFeed()
    {
        using var service = _customerHost.StartService("Production");
        var feeds = service.Database.GetCollection<CalendarFeedEntity>("calendar_feeds");
        await feeds.InsertOneAsync(new CalendarFeedEntity(Customer, CalendarFeedToken.Hash(CalendarFeedToken.New()), "en"));

        var request = new HttpRequestMessage(HttpMethod.Delete, $"api/v1/customers/{Customer}");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", _tokens.CreateToken(Customer, TokenFactory.CustomerRole));
        var response = await service.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await feeds.Find(new BsonDocument("owner_email", Customer)).ToListAsync());
    }
}
