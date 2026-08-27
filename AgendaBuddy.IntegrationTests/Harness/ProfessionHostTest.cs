using System.Net;
using AgendaBuddy.Library.Entities;
using MongoDB.Bson;

namespace AgendaBuddy.IntegrationTests.Harness;

/// <summary>
/// A real service, a real route, a real HTTP request, a real MongoDB Testcontainer.
/// </summary>
/// <remarks>
/// <para>
/// This is the first test in the solution to execute a route table. `11-testing.md:148` records the
/// gap it closes: <i>"Program.cs is not coverable… there is no integration test in the solution. Every
/// endpoint's auth attribute, validation call, ownership guard, and status-code mapping is unverified
/// end-to-end."</i> That gap is why the Calendar IDOR could exist unnoticed, and why the
/// harness exists rather than being assumed.
/// </para>
/// <para>
/// <b>Profession, and anonymously, on purpose.</b> AC-18 requires the two profession read routes to
/// stay anonymous, so this proves the route with no <c>Authorization</c> header at all — reaching for
/// an authenticated route instead would introduce a needless dependency on token creation.
/// </para>
/// </remarks>
[Collection(HarnessCollection.Name)]
public class ProfessionHostTest(ServiceHostFixture<ProfessionAnchor> host)
    : IClassFixture<ServiceHostFixture<ProfessionAnchor>>
{
    [Fact]
    public async Task GetProfessions_ReturnsOkAnonymously_FromTheContainerBackedService()
    {
        using var service = host.StartService();

        var response = await service.Client.GetAsync("api/v1/professions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void EachTest_GetsItsOwnDatabase_InsideTheSharedContainer()
    {
        // ADR-017's other half: one container per class, one database per test. Two services started
        // from the same fixture must not share state, or tests silently depend on each other's data.
        using var first = host.StartService();
        using var second = host.StartService();

        Assert.NotEqual(first.DatabaseName, second.DatabaseName);
        Assert.StartsWith("itest_", first.DatabaseName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheServiceReadsAndWritesTheContainer_NotSomeOtherDatabase()
    {
        // Proves the container is actually wired in rather than the service falling back to a default:
        // a document inserted directly into this test's database is visible through the HTTP route.
        using var service = host.StartService();

        var profession = new ProfessionEntity
        {
            Id = ObjectId.GenerateNewId(),
            Name = $"harness-probe-{Guid.NewGuid():N}",
        };

        await service.Database
            .GetCollection<ProfessionEntity>("professions")
            .InsertOneAsync(profession);

        var response = await service.Client.GetAsync($"api/v1/professions/{profession.Name}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(profession.Name, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }
}
