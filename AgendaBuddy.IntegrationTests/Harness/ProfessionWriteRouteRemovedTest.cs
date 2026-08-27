using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AgendaBuddy.Library.Entities;
using MongoDB.Driver;

namespace AgendaBuddy.IntegrationTests.Harness;

/// <summary>
/// F-016 AC-18 and AC-26 (`[security]`, threat <b>T-007</b>): <c>POST /api/v1/professions</c> no longer
/// exists, and the two profession read routes are still anonymous.
/// </summary>
/// <remarks>
/// <para>
/// <b>Deleted rather than role-gated, and requirement 13 is superseded (ADR-025).</b> There is no role to
/// check for: Identity's allow-list is exactly <c>{Provider, Customer}</c>
/// (<c>AgendaBuddy.Identity/Program.cs:121</c>) — there is no administrative tier. The only implementable check,
/// <c>AssertRole(user, "Provider")</c>, would still let any self-registered provider write to global
/// reference data read by every user; with open, unthrottled registration that raises the bar from "any
/// account" to "any account that picked Provider at signup".
/// </para>
/// <para>
/// Professions are <b>seeded</b> from <c>Library/Data/ProfessionSeedData.cs</c> and no shipped flow creates
/// one, so nothing is lost. If professions ever need to be user-creatable, that is a feature with a real
/// authorization model — not a route quietly restored.
/// </para>
/// <para>
/// <b>Both roles are tried.</b> The point of T-007 is that <em>no</em> authenticated caller can write, so
/// testing one role would leave the other unexamined — and a role check is exactly the wrong fix someone
/// might reintroduce.
/// </para>
/// </remarks>
[Collection(HarnessCollection.Name)]
public class ProfessionWriteRouteRemovedTest : IClassFixture<ServiceHostFixture<ProfessionAnchor>>
{
    private readonly ServiceHostFixture<ProfessionAnchor> _host;
    private readonly TokenFactory _tokens;

    public ProfessionWriteRouteRemovedTest(
        ServiceHostFixture<ProfessionAnchor> host,
        CryptoSessionFixture crypto)
    {
        _host = host;
        _tokens = new TokenFactory(crypto);
    }

    [Theory]
    [InlineData(TokenFactory.ProviderRole)]
    [InlineData(TokenFactory.CustomerRole)]
    public async Task T007_TheRouteIsGone_AndNoProfessionIsCreatedByAnyRole(string role)
    {
        using var service = _host.StartService("Production");

        var name = $"smuggled-profession-{Guid.NewGuid():N}";
        var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/professions")
        {
            Content = JsonContent.Create(new { Name = name }),
            Headers =
            {
                Authorization = new AuthenticationHeaderValue(
                    "Bearer", _tokens.CreateToken("caller@example.com", role)),
            },
        };

        var response = await service.Client.SendAsync(request);

        Assert.True(
            response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
            $"expected 404 or 405 because the route should not exist; got {(int)response.StatusCode}");

        // Status codes alone would not prove nothing was written -- a 404 returned *after* an insert would
        // look identical from outside. So the collection is checked directly.
        var written = await service.Database
            .GetCollection<ProfessionEntity>("professions")
            .Find(Builders<ProfessionEntity>.Filter.Eq(p => p.Name, name))
            .AnyAsync();

        Assert.False(written, $"a profession named {name} was created despite the route being removed");
    }

    [Fact]
    public async Task T007_AC18_BothProfessionReadRoutesStillReturn200Anonymously()
    {
        // The other half of AC-26 and the whole of AC-18. Reference data with no PII must stay open, and
        // deleting a sibling route in the same MapGroup is a plausible way to break it by accident.
        using var service = _host.StartService("Production");

        var seeded = await service.Database
            .GetCollection<ProfessionEntity>("professions")
            .Find(Builders<ProfessionEntity>.Filter.Empty)
            .FirstOrDefaultAsync();

        Assert.NotNull(seeded);

        var list = await service.Client.GetAsync("api/v1/professions");
        var single = await service.Client.GetAsync($"api/v1/professions/{seeded!.Name}");

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(HttpStatusCode.OK, single.StatusCode);
    }
}
