using System.Net;
using System.Net.Http.Headers;

namespace AgendaBuddy.IntegrationTests.Harness;

/// <summary>
/// The five PII-bearing GET routes require authorization.
/// </summary>
/// <remarks>
/// <para>
/// This is the headline defect of the feature. <c>GET /api/v1/providers</c> returned every provider's full
/// record — including embedded appointments carrying customer emails, and each provider's subscribed
/// customer list — <b>anonymously and unpaginated</b>.
/// </para>
/// <para>
/// <b>The count is five, not four.</b> <c>GET /api/v1/services/{email}</c> was omitted from the program
/// Discover summary and added at Define.
/// </para>
/// <para>
/// <b>Safe as a breaking change</b>, because these routes have zero reachable consumers: the mobile
/// client's paths all omit <c>api/v1/</c> and its single <c>ApiBaseUrl</c> cannot address seven processes
/// (<c>01-api-surface.md:158</c>). Confirmed at the PRD gate that authenticating provider discovery matches
/// product intent — the ROADMAP defines the flow as "a customer <b>signs up</b>,
/// discovers providers, and subscribes to one", which makes discovery post-signup by the product's own
/// definition.
/// </para>
/// <para>
/// The authenticated cases are asserted too. Without them, a change that broke these routes outright would
/// satisfy every 401 assertion and look like a security win.
/// </para>
/// </remarks>
[Collection(HarnessCollection.Name)]
public class AnonymousPiiRoutesTest :
    IClassFixture<ServiceHostFixture<ProviderAnchor>>,
    IClassFixture<ServiceHostFixture<CustomerAnchor>>,
    IClassFixture<ServiceHostFixture<ServicesAnchor>>
{
    private const string Caller = "caller@example.com";

    private readonly ServiceHostFixture<ProviderAnchor> _provider;
    private readonly ServiceHostFixture<CustomerAnchor> _customer;
    private readonly ServiceHostFixture<ServicesAnchor> _services;
    private readonly TokenFactory _tokens;

    public AnonymousPiiRoutesTest(
        ServiceHostFixture<ProviderAnchor> provider,
        ServiceHostFixture<CustomerAnchor> customer,
        ServiceHostFixture<ServicesAnchor> services,
        CryptoSessionFixture crypto)
    {
        _provider = provider;
        _customer = customer;
        _services = services;
        _tokens = new TokenFactory(crypto);
    }

    public static TheoryData<string, string> PiiRoutes() => new()
    {
        { "provider", "api/v1/providers" },
        { "provider", $"api/v1/providers/{Caller}" },
        { "customer", "api/v1/customers" },
        { "customer", $"api/v1/customers/{Caller}" },
        { "services", $"api/v1/services/{Caller}" },
    };

    private ServiceHost StartFor(string service) => service switch
    {
        "provider" => _provider.StartService(),
        "customer" => _customer.StartService(),
        "services" => _services.StartService(),
        _ => throw new ArgumentOutOfRangeException(nameof(service), service, "unknown service"),
    };

    [Theory]
    [MemberData(nameof(PiiRoutes))]
    public async Task AC8_AnUnauthenticatedRequestGets401(string service, string route)
    {
        using var host = StartFor(service);

        var response = await host.Client.GetAsync(route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(PiiRoutes))]
    public async Task AnAuthenticatedRequestIsNotRefused(string service, string route)
    {
        // The control: these routes must still work for a legitimate caller. 401 or 403 here would mean
        // T12 broke them rather than secured them, and every assertion above would still pass.
        using var host = StartFor(service);

        var request = new HttpRequestMessage(HttpMethod.Get, route);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", _tokens.CreateToken(Caller, TokenFactory.ProviderRole));

        var response = await host.Client.SendAsync(request);

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
