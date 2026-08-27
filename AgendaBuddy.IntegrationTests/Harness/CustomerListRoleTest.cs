using System.Net;
using System.Net.Http.Headers;
using AgendaBuddy.Library.Entities;
using MongoDB.Bson;

namespace AgendaBuddy.IntegrationTests.Harness;

/// <summary>
/// F-016 AC-22 (`[security]`, threat <b>T-003</b>, HIGH): <c>GET /api/v1/customers</c> requires the
/// <c>Provider</c> role, not merely a token.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why authentication alone was nearly worthless here.</b> <c>POST /api/v1/auth/register</c> is
/// anonymous, unverified and unrate-limited, so an attacker self-registers as a Customer, obtains a valid
/// token, and pages through the entire customer table exactly as before — with <c>totalCount</c> helpfully
/// reporting how many pages to fetch. <b>Pagination bounds the response, not the extraction.</b> This is a
/// scope addition beyond the approved PRD, escalated and accepted at the threat party (ADR-026).
/// </para>
/// <para>
/// Reframed by Atlas as a product question rather than a control question: <em>who is this endpoint for?</em>
/// F-003 defines discovery as customers finding <b>providers</b>, not each other. No shipped flow lists
/// every customer, so the only defensible caller is a provider.
/// </para>
/// <para>
/// <b>Only the list route.</b> <c>GET /api/v1/customers/{email}</c> stays authenticated-but-not-role-gated,
/// because a customer legitimately reads their own record through it. Asserted below, so a later "tidy-up"
/// cannot role-gate it and lock customers out of their own data.
/// </para>
/// <para>
/// This also brings <c>OwnershipGuard.AssertRole</c> into use for the first time — it existed but was
/// called from nowhere (<c>ARCHITECTURE.md</c> §3.2c).
/// </para>
/// <para>
/// <b>Deferred, not rejected:</b> scoping results to the calling provider's own
/// <c>SubscribedCustomerCollection</c> is the stronger fix and was weighed at the gate. It is a real
/// behaviour change and more work; the role check blocks the actual attack path now.
/// </para>
/// </remarks>
[Collection(HarnessCollection.Name)]
public class CustomerListRoleTest : IClassFixture<ServiceHostFixture<CustomerAnchor>>
{
    private const string CustomerEmail = "a-real-customer@example.com";

    private readonly ServiceHostFixture<CustomerAnchor> _host;
    private readonly TokenFactory _tokens;

    public CustomerListRoleTest(ServiceHostFixture<CustomerAnchor> host, CryptoSessionFixture crypto)
    {
        _host = host;
        _tokens = new TokenFactory(crypto);
    }

    private async Task<ServiceHost> StartWithACustomerOnFile(string environment = "Production")
    {
        var service = _host.StartService(environment);

        await service.Database.GetCollection<CustomerEntity>("customers").InsertOneAsync(new CustomerEntity
        {
            Id = ObjectId.GenerateNewId(),
            FirstName = "Ada",
            LastName = "Lovelace",
            Email = CustomerEmail,
        });

        return service;
    }

    private HttpRequestMessage Get(string route, string role, string subject = "caller@example.com")
    {
        var request = new HttpRequestMessage(HttpMethod.Get, route);
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _tokens.CreateToken(subject, role));
        return request;
    }

    [Fact]
    public async Task T003_ACustomerRoleTokenGets403AndNoCustomerRecord()
    {
        using var service = await StartWithACustomerOnFile();

        var response = await service.Client.SendAsync(Get("api/v1/customers", TokenFactory.CustomerRole));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // "and returns no customer record" -- asserted by searching for the seeded value, not by trusting
        // that a 403 has an empty body.
        Assert.DoesNotContain(CustomerEmail, body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Lovelace", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AProviderRoleTokenIsAllowedToListCustomers()
    {
        // The control. Without it, a change that refused everybody would satisfy the 403 assertion.
        using var service = await StartWithACustomerOnFile();

        var response = await service.Client.SendAsync(Get("api/v1/customers", TokenFactory.ProviderRole));

        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ACustomerCanStillReadTheirOwnRecordByEmail()
    {
        // Only the LIST route is role-gated. If someone later "tidies up" by putting AssertRole on the
        // group, this fails -- which is the point: it would lock every customer out of their own data.
        using var service = await StartWithACustomerOnFile();

        var response = await service.Client.SendAsync(
            Get($"api/v1/customers/{CustomerEmail}", TokenFactory.CustomerRole, CustomerEmail));

        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
