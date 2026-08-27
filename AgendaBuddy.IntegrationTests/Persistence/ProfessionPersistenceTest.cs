using System.Net;
using System.Text.Json;
using AgendaBuddy.IntegrationTests.Harness;
using AgendaBuddy.Library.Data;

namespace AgendaBuddy.IntegrationTests.Persistence;

/// <summary>
/// Profession has no write ROUTE any more — <c>POST /api/v1/professions</c> was deleted
/// (<c>ProfessionWriteRouteRemovedTest</c> pins that). Its only write is
/// <see cref="Profession.Extensions.ProfessionSeedHostedService"/>, which inserts
/// <see cref="ProfessionSeedData.SeedData"/> into an empty collection at startup — so this test follows
/// the same "SEED then READ" shape as Calendar's, just triggered by host startup
/// rather than by this test inserting directly.
/// </summary>
[Collection(HarnessCollection.Name)]
public class ProfessionPersistenceTest(ServiceHostFixture<ProfessionAnchor> host)
    : IClassFixture<ServiceHostFixture<ProfessionAnchor>>
{
    [Fact]
    public async Task AC6_TheStartupSeed_ReadsBackFromBothProfessionRoutes()
    {
        using var service = host.StartService("Production");

        var expectedCount = ProfessionSeedData.SeedData().Count;

        var listResponse = await service.Client.GetAsync("api/v1/professions");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        // Parsed field-by-field rather than deserialised into ProfessionEntity: Profession does not
        // register ObjectIdJsonConverter (per ObjectIdJsonConverter's own remarks), so its "id" field is
        // the unusable {timestamp,machine,...} shape client-side deserialisation cannot parse. The only
        // field this test cares about is "name".
        //
        // The response is wrapped in DataResponse<T> (ADR-049) -- the array/object is under a
        // "data" property, not the response root.
        using var listBody = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        var professionNames = listBody.RootElement.GetProperty("data").EnumerateArray()
            .Select(element => element.GetProperty("name").GetString())
            .ToList();
        Assert.Equal(expectedCount, professionNames.Count);
        Assert.Contains("Coaching", professionNames);

        var byNameResponse = await service.Client.GetAsync("api/v1/professions/Coaching");
        Assert.Equal(HttpStatusCode.OK, byNameResponse.StatusCode);

        using var professionBody = JsonDocument.Parse(await byNameResponse.Content.ReadAsStringAsync());
        Assert.Equal("Coaching", professionBody.RootElement.GetProperty("data").GetProperty("name").GetString());
    }
}
