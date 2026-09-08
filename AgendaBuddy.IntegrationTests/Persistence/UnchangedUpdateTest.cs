using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AgendaBuddy.Library.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AgendaBuddy.IntegrationTests.Persistence;

/// <summary>
/// Saving a record without changing any value is a success, not a failure.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>This can only be caught against a real database.</b> MongoDB reports <c>ModifiedCount == 0</c> for a
/// replace whose document is byte-identical to the stored one, and <c>MongoDbRepository.UpdateAsync</c> used to
/// read that as failure — indistinguishable from "no such document". A mocked repository returns whatever the test
/// tells it to, so no unit test could see it.
/// </para>
/// <para>
/// The symptom was a screen that could never be saved. The profile editor's natural flow is: open it, tick the two
/// agreement checkboxes, Save. That leaves the name and the phone untouched, so the whole-document replace behind
/// <c>PUT /api/v1/customers/{email}</c> changed nothing, the route answered <c>404</c>, and the user was told
/// "could not save your profile — try again" on a request that had in fact done exactly what was asked. Worse, the
/// consent write is sequenced after the profile write, so the agreements never landed either.
/// </para>
/// </remarks>
[Collection(Harness.HarnessCollection.Name)]
public class UnchangedUpdateTest : IClassFixture<Harness.ServiceHostFixture<CustomerAnchor>>
{
    private const string Email = "unchanged@example.com";

    private readonly Harness.ServiceHostFixture<CustomerAnchor> _host;
    private readonly Harness.TokenFactory _tokens;

    public UnchangedUpdateTest(
        Harness.ServiceHostFixture<CustomerAnchor> host, Harness.CryptoSessionFixture crypto)
    {
        _host = host;
        _tokens = new Harness.TokenFactory(crypto);
    }

    private HttpRequestMessage Put(object body) =>
        new(HttpMethod.Put, $"api/v1/customers/{Email}")
        {
            Content = JsonContent.Create(body),
            Headers = { Authorization = new AuthenticationHeaderValue(
                "Bearer", _tokens.CreateToken(Email, Harness.TokenFactory.CustomerRole)) }
        };

    private static CustomerEntity Seed() => new()
    {
        Id = ObjectId.GenerateNewId(),
        FirstName = "Grace",
        LastName = "Hopper",
        Email = Email,
        PhoneNumber = "+15550199"
    };

    /// <summary>
    /// The regression test. Every field is sent back exactly as stored, so MongoDB modifies nothing.
    /// </summary>
    [Fact]
    public async Task PuttingBackTheIdenticalDocumentSucceeds()
    {
        using var service = _host.StartService("Production");
        await service.Database.GetCollection<CustomerEntity>("customers").InsertOneAsync(Seed());

        var response = await service.Client.SendAsync(Put(new
        {
            email = Email,
            firstName = "Grace",
            lastName = "Hopper",
            phoneNumber = "+15550199"
        }));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    /// <summary>The ordinary case still works, so the fix did not make every write look successful.</summary>
    [Fact]
    public async Task ChangingAValueStillSucceeds()
    {
        using var service = _host.StartService("Production");
        await service.Database.GetCollection<CustomerEntity>("customers").InsertOneAsync(Seed());

        var response = await service.Client.SendAsync(Put(new
        {
            email = Email,
            firstName = "Grace",
            lastName = "Hopper-Murray",
            phoneNumber = "+15550199"
        }));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var stored = await service.Database.GetCollection<CustomerEntity>("customers")
            .Find(Builders<CustomerEntity>.Filter.Eq(c => c.Email, Email)).SingleAsync();
        Assert.Equal("Hopper-Murray", stored.LastName);
    }

    /// <summary>
    /// ⚠️ <b>And a genuinely missing record must still be a 404.</b> This is the assertion that keeps the fix from
    /// having simply replaced one wrong answer with another: <c>MatchedCount</c> is zero when nothing matched, so
    /// "no such customer" is still distinguishable from "nothing to change".
    /// </summary>
    [Fact]
    public async Task PuttingToAnAccountThatDoesNotExistIsStillNotFound()
    {
        using var service = _host.StartService("Production");

        var response = await service.Client.SendAsync(Put(new
        {
            email = Email,
            firstName = "Grace",
            lastName = "Hopper"
        }));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
