using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace AgendaBuddy.IntegrationTests.Harness;

/// <summary>
/// <c>POST /api/v1/providers</c> requires the <c>Provider</c> role <b>and</b> that the record
/// being created is the caller's own.
/// </summary>
/// <remarks>
/// <para>
/// <b>Both arms are required and each has its own test.</b> A role check alone still lets one Provider
/// create a record for another provider's email — which is account takeover by registration, not a
/// validation nicety. An ownership check alone would let a Customer create provider records for themselves.
/// </para>
/// <para>
/// This is one of only two <c>AssertRole</c> call sites in the solution.
/// <c>13-security.md:137</c>: <c>AssertRole</c> had <b>never been called anywhere</b>, so the <c>role</c>
/// claim authorized nothing at all before this feature.
/// </para>
/// <para>
/// Expect this to <em>surface</em> latent breakage rather than cause it: <c>SeedAuthCredentials</c> is dead
/// code, so any pre-auth provider record has no credential and cannot authenticate. That is expected.
/// </para>
/// </remarks>
[Collection(HarnessCollection.Name)]
public class ProviderCreationGuardTest : IClassFixture<ServiceHostFixture<ProviderAnchor>>
{
    private const string Caller = "the-caller@example.com";
    private const string SomeoneElse = "someone-else@example.com";

    private readonly ServiceHostFixture<ProviderAnchor> _host;
    private readonly TokenFactory _tokens;

    public ProviderCreationGuardTest(ServiceHostFixture<ProviderAnchor> host, CryptoSessionFixture crypto)
    {
        _host = host;
        _tokens = new TokenFactory(crypto);
    }

    private HttpRequestMessage CreateProvider(string forEmail, string callerSubject, string role)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/providers")
        {
            // Valid per ProviderEntity's [Required]/[EmailAddress] annotations, so MiniValidator passes and
            // execution reaches the guards. A unique surname avoids the pre-existing duplicate-name branch.
            Content = JsonContent.Create(new
            {
                FirstName = "Grace",
                LastName = $"Hopper-{Guid.NewGuid():N}",
                Email = forEmail,
            }),
        };

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _tokens.CreateToken(callerSubject, role));

        return request;
    }

    [Fact]
    public async Task AC11_ACustomerRoleCallerCannotCreateAProvider()
    {
        using var service = _host.StartService("Production");

        var response = await service.Client.SendAsync(
            CreateProvider(forEmail: Caller, callerSubject: Caller, role: TokenFactory.CustomerRole));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AC11_AProviderCannotCreateARecordForSomebodyElsesEmail()
    {
        // The arm a role check alone would miss. Without it, any provider can register a record under
        // another provider's email address.
        using var service = _host.StartService("Production");

        var response = await service.Client.SendAsync(
            CreateProvider(forEmail: SomeoneElse, callerSubject: Caller, role: TokenFactory.ProviderRole));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AProviderCreatingTheirOwnRecordIsAllowed()
    {
        // The control. Without it, a guard that refused every POST would satisfy both assertions above.
        //
        // ⚠️ It asserts "not refused by AUTHORIZATION", not "created". This route actually returns 400 in
        // the harness, for a reason unrelated to authorization: the handler publishes to
        // Kafka, there is no broker here, and KafkaClient reports the failure as a validation problem
        // (Provider/Program.cs's Kafka error branch). Asserting 201 would be asserting that a Kafka broker
        // exists. Recorded so the weaker assertion reads as deliberate.
        using var service = _host.StartService("Production");

        var response = await service.Client.SendAsync(
            CreateProvider(forEmail: Caller, callerSubject: Caller, role: TokenFactory.ProviderRole));

        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
