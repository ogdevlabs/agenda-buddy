using System.Net;
using System.Net.Http.Headers;
using AgendaBuddy.Library.Entities;
using MongoDB.Bson;

namespace AgendaBuddy.IntegrationTests.Harness;

/// <summary>
/// Provider reads are projected to <c>ProviderSummary</c> for anyone who is
/// not the owning provider. Also carries the route-level half of the null-claim ownership case.
/// </summary>
/// <remarks>
/// <para>
/// <b>Authentication alone does not fix this.</b> <c>ProviderEntity</c> embeds <c>ServiceEntities</c>,
/// <c>AppointmentEntities</c> — each carrying <c>email_customer</c> — and <c>SubscribedCustomerCollection</c>.
/// An authenticated <em>customer</em> browsing for a coach would still receive every provider's appointment
/// book and client roster. Requirement 10 holds regardless of the authentication decision.
/// </para>
/// <para>
/// ⚠️ The projection selects owner-vs-non-owner
/// with <c>OwnershipGuard.AssertOwner</c>, whose null-claim fall-through used to land on the <em>owner</em>
/// branch — so a token with no <c>sub</c> would have received the unprojected entity.
/// </para>
/// </remarks>
[Collection(HarnessCollection.Name)]
public class ProviderProjectionTest : IClassFixture<ServiceHostFixture<ProviderAnchor>>
{
    private const string Owner = "projected-provider@example.com";
    private const string Browser = "curious-customer@example.com";
    private const string ClientInTheBook = "the-providers-client@example.com";

    private readonly ServiceHostFixture<ProviderAnchor> _host;
    private readonly TokenFactory _tokens;

    public ProviderProjectionTest(ServiceHostFixture<ProviderAnchor> host, CryptoSessionFixture crypto)
    {
        _host = host;
        _tokens = new TokenFactory(crypto);
    }

    private async Task<ServiceHost> StartWithAProviderOnFile()
    {
        var service = _host.StartService("Production");

        await service.Database.GetCollection<ProviderEntity>("providers").InsertOneAsync(new ProviderEntity
        {
            Id = ObjectId.GenerateNewId(),
            FirstName = "Sarah",
            LastName = "Nakamura",
            Email = Owner,
            KafkaTopic = "provider-topic-that-must-not-leak",
            ServiceEntities = [new ServiceEntity { Id = ObjectId.GenerateNewId(), Name = "60-min PT session", Fee = 65m }],
            AppointmentEntities =
            [
                new AppointmentEntity
                {
                    Id = ObjectId.GenerateNewId(),
                    EmailProvider = Owner,
                    EmailCustomer = ClientInTheBook,
                    Start = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc),
                    End = new DateTime(2026, 9, 1, 11, 0, 0, DateTimeKind.Utc),
                },
            ],
            SubscribedCustomerCollection = [ClientInTheBook],
        });

        return service;
    }

    private HttpRequestMessage Read(string route, string? callerSubject)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, route);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            callerSubject is null
                ? _tokens.CreateTokenWithoutSubject(TokenFactory.CustomerRole)
                : _tokens.CreateToken(callerSubject, TokenFactory.CustomerRole));
        return request;
    }

    private static void AssertNoEmbeddedData(string body)
    {
        Assert.DoesNotContain(ClientInTheBook, body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("appointment", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("subscribed", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("kafka", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AC9_ANonOwnerReadingTheListGetsNoAppointmentsOrCustomers()
    {
        using var service = await StartWithAProviderOnFile();

        var response = await service.Client.SendAsync(Read("api/v1/providers", Browser));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // The public profile is still there — this is a projection, not a refusal. The discovery flow
        // depends on a customer being able to browse providers.
        Assert.Contains("Nakamura", body, StringComparison.Ordinal);
        Assert.Contains("60-min PT session", body, StringComparison.Ordinal);

        AssertNoEmbeddedData(body);
    }

    [Fact]
    public async Task AC9_ANonOwnerReadingOneProviderGetsNoAppointmentsOrCustomers()
    {
        using var service = await StartWithAProviderOnFile();

        var response = await service.Client.SendAsync(Read($"api/v1/providers/{Owner}", Browser));
        var body = await response.Content.ReadAsStringAsync();

        // Deliberately 200, not 403: requirement 10 makes it safe to READ another provider's summary —
        // that is the discovery flow. Only the embedded data is withheld.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Nakamura", body, StringComparison.Ordinal);
        AssertNoEmbeddedData(body);
    }

    [Fact]
    public async Task AC9_TheOwningProviderStillGetsTheirOwnFullRecord()
    {
        // The control, and the actual requirement: owners must not lose access to their own appointment
        // book. A projection applied unconditionally would satisfy every assertion above and break the
        // provider's own app.
        using var service = await StartWithAProviderOnFile();

        var response = await service.Client.SendAsync(Read($"api/v1/providers/{Owner}", Owner));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(ClientInTheBook, body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task T001_ATokenWithNoSubjectNeverReceivesTheFullProviderEntity()
    {
        // AC-21's route half, deferred here from T09 because this route was neither authenticated nor
        // projected at that point. Before T09's fix, AssertOwner(user, email) with a null sub claim fell
        // through to the OWNER branch, so exactly this request would have returned the unprojected entity
        // including the appointment book.
        using var service = await StartWithAProviderOnFile();

        var response = await service.Client.SendAsync(Read($"api/v1/providers/{Owner}", callerSubject: null));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertNoEmbeddedData(body);
    }
}
