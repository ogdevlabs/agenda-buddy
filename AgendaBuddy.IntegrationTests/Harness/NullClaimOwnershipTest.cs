using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace AgendaBuddy.IntegrationTests.Harness;

/// <summary>
/// At the route level: a token with no
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
/// <c>GET /api/v1/providers/{email}</c> "never returns the full <c>ProviderEntity</c>" for such a token;
/// that assertion lives with the route's own authorization and projection tests.
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
        // guard was never exercised — which would make this test prove nothing.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
