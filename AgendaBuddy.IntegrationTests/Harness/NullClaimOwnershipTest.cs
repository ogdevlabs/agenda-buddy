using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace AgendaBuddy.IntegrationTests.Harness;

/// <summary>
/// F-016 AC-21 (`[security]`, threat <b>T-001</b>, HIGH) at the route level: a token with no
/// <c>sub</c> claim is not an owner.
/// </summary>
/// <remarks>
/// <para>
/// The unit-level pins live in <c>Library.Tests/Tools/OwnershipGuardTest.cs</c>
/// (<c>T001_*</c>). This is the part that could only ever be asserted through the harness: a
/// well-formed, correctly signed, unexpired token that simply carries no subject, sent at a real
/// ownership-guarded route over real HTTP. <c>TokenFactory.CreateTokenWithoutSubject()</c> exists for
/// exactly this.
/// </para>
/// <para>
/// ⚠️ <b>AC-21's other half is not attested here.</b> The criterion also requires that
/// <c>GET /api/v1/providers/{email}</c> "never returns the full <c>ProviderEntity</c>" for such a token.
/// That route is <b>still anonymous and still unprojected</b> at this point in the build — F-016-T12 adds
/// the authorization and F-016-T11 adds the <c>ProviderSummary</c> projection. Writing that assertion now
/// would leave a failing test in the tree for several tasks, so it is authored under <b>T11</b>, which is
/// where the behaviour arrives. T09 fixes the primitive the projection depends on; T11 proves the route.
/// Recorded so the gap is a sequencing decision rather than something lost between two tasks, and
/// F-016-T19's attestation must check both halves.
/// </para>
/// </remarks>
[Collection(HarnessCollection.Name)]
public class NullClaimOwnershipTest : IClassFixture<ServiceHostFixture<CustomerAnchor>>
{
    private const string Owner = "owner@example.com";

    private readonly ServiceHostFixture<CustomerAnchor> _host;
    private readonly TokenFactory _tokens;

    public NullClaimOwnershipTest(ServiceHostFixture<CustomerAnchor> host, CryptoSessionFixture crypto)
    {
        _host = host;
        _tokens = new TokenFactory(crypto);
    }

    [Fact]
    public async Task T001_ATokenWithNoSubjectIsAuthenticatedButIsNeverAnOwner()
    {
        using var service = _host.StartService("Production");

        var request = new HttpRequestMessage(HttpMethod.Put, $"api/v1/customers/{Owner}")
        {
            // Valid body: MiniValidator runs before the ownership guard, so an invalid one would return
            // 400 and never reach the code under test.
            Content = JsonContent.Create(new { FirstName = "Ada", LastName = "Lovelace", Email = Owner }),
            Headers =
            {
                Authorization = new AuthenticationHeaderValue(
                    "Bearer", _tokens.CreateTokenWithoutSubject(TokenFactory.CustomerRole)),
            },
        };

        var response = await service.Client.SendAsync(request);

        // 403, not 401: the token is genuinely valid, so authentication succeeds and the request is
        // refused by the ownership guard. A 401 here would mean the token was rejected outright and the
        // guard was never exercised — which would make this test prove nothing about T-001.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
