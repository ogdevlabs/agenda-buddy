using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AgendaBuddy.IntegrationTests.Harness;
using AgendaBuddy.IntegrationTests.Persistence;
using AgendaBuddy.EventAndCommands.Persistence;
using AgendaBuddy.Library.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AgendaBuddy.IntegrationTests.Audit;

/// <summary>
/// F-018-T13 / AC-7. <c>UpdateCustomerCommandHandler</c> (success) and <c>AddCustomerCommandHandler</c>
/// (failure) both write a <c>CustomerService</c>-side audit event through <see cref="IEventStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why two different commands rather than one command's two branches.</b> <c>PUT</c> (update) needs no
/// Kafka broker and this harness runs none, so it is the reachable SUCCESS path — exactly the reasoning
/// <c>CustomerPersistenceTest</c> already recorded for the same route. <c>POST</c> (create) always calls
/// <c>KafkaClient.CreateTopicIfNotExist</c> against an address nothing is listening on
/// (<c>ProviderCreationGuardTest</c> records the identical constraint for <c>POST /providers</c>), so it is
/// a reliable, reachable FAILURE path for a different handler in the same service — not a contrived one.
/// </para>
/// <para>
/// ⚠️ <b>Not <c>UpdateCustomerCommandHandler</c>'s own failure branch.</b> A missing customer causes
/// <c>UpdateCustomerCommandHandler.cs:51-59</c> to write a <c>Failed</c> event whose <c>Type</c> is
/// literally <c>"UpdateProviderCommand"</c> — a pre-existing copy-paste defect in production code, out of
/// this task's scope to fix (see F-018-T13's own scope discipline). Using the cross-handler pair above
/// avoids pinning that mislabelled string as if it were the intended contract.
/// </para>
/// </remarks>
[Collection(HarnessCollection.Name)]
public class CustomerAuditTest(ServiceHostFixture<CustomerAnchor> host, CryptoSessionFixture crypto)
    : IClassFixture<ServiceHostFixture<CustomerAnchor>>
{
    private const string Email = "customer-audit@example.com";

    private readonly TokenFactory _tokens = new(crypto);

    private static IMongoCollection<Event> Events(ServiceHost service) =>
        ConfiguredCollection.Of<Event>(service, "EventsCollection", "events");

    [Fact]
    public async Task AC7_AnUpdatedCustomer_WritesASuccessAuditEvent()
    {
        using var service = host.StartService("Production");

        await ConfiguredCollection.Of<CustomerEntity>(service, "CustomersCollection", "customers")
            .InsertOneAsync(new CustomerEntity
            {
                Id = ObjectId.GenerateNewId(),
                FirstName = "Before",
                LastName = "Audit",
                Email = Email,
            });

        var request = new HttpRequestMessage(HttpMethod.Put, $"api/v1/customers/{Email}")
        {
            Content = JsonContent.Create(new { FirstName = "After", LastName = "Audit", Email }),
            Headers =
            {
                Authorization = new AuthenticationHeaderValue(
                    "Bearer", _tokens.CreateToken(Email, TokenFactory.CustomerRole)),
            },
        };

        var response = await service.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var audit = await Events(service)
            .Find(Builders<Event>.Filter.Eq(e => e.Type, "UpdateCustomerCommand"))
            .SingleOrDefaultAsync();

        Assert.NotNull(audit);
        Assert.Equal("Success", audit.Status);
    }

    [Fact]
    public async Task AC7_ACreateWithNoKafkaBrokerReachable_WritesAFailedAuditEvent()
    {
        using var service = host.StartService("Production");

        var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/customers")
        {
            Content = JsonContent.Create(new
            {
                FirstName = "Audit",
                LastName = $"Create-{Guid.NewGuid():N}",
                Email = $"customer-audit-create-{Guid.NewGuid():N}@example.com",
            }),
            Headers =
            {
                Authorization = new AuthenticationHeaderValue(
                    "Bearer", _tokens.CreateToken(Email, TokenFactory.CustomerRole)),
            },
        };

        var response = await service.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var audit = await Events(service)
            .Find(Builders<Event>.Filter.Regex(e => e.Type, new BsonRegularExpression("^AddCustomerCommand")))
            .SingleOrDefaultAsync();

        Assert.NotNull(audit);
        Assert.Equal("Failed", audit.Status);
        Assert.StartsWith("AddCustomerCommand - Exception", audit.Type);
    }
}
