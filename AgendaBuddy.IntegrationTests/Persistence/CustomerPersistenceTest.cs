using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AgendaBuddy.IntegrationTests.Harness;
using Library.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AgendaBuddy.IntegrationTests.Persistence;

/// <summary>
/// F-018-T12 / AC-6. <c>PUT /api/v1/customers/{email}</c> followed by <c>GET /api/v1/customers/{email}</c>
/// proves <see cref="CustomerEntity"/>'s <c>[BsonElement]</c> mapping round-trips through a real write and
/// a real read.
/// </summary>
/// <remarks>
/// <b>Not <c>POST /api/v1/customers</c>.</b> That route only inserts once
/// <c>KafkaClient.CreateTopicIfNotExist</c> reports success (<c>Customer/Program.cs:155-162</c>), and this
/// harness starts no Kafka broker — <c>ProviderCreationGuardTest</c> already records the identical
/// constraint for <c>POST /api/v1/providers</c>. <c>PUT</c> has no such dependency
/// (<c>UpdateCustomerCommandHandler</c> calls no Kafka client), so a seeded starting document plus a real
/// <c>PUT</c> is the write this suite can actually exercise end to end.
/// </remarks>
[Collection(HarnessCollection.Name)]
public class CustomerPersistenceTest(ServiceHostFixture<CustomerAnchor> host, CryptoSessionFixture crypto)
    : IClassFixture<ServiceHostFixture<CustomerAnchor>>
{
    private const string Email = "customer-round-trip@example.com";

    private readonly TokenFactory _tokens = new(crypto);

    [Fact]
    public async Task AC6_AnUpdatedCustomer_ReadsBackWithEveryFieldIntact()
    {
        using var service = host.StartService("Production");

        var customers = ConfiguredCollection.Of<CustomerEntity>(service, "CustomersCollection", "customers");
        await customers.InsertOneAsync(new CustomerEntity
        {
            Id = ObjectId.GenerateNewId(),
            FirstName = "Before",
            LastName = "Update",
            Email = Email,
            KafkaTopic = "seeded-customer-topic",
            SubscribedProviderCollection = ["seeded-provider@example.com"],
            AppointmentCollection = ["seeded-appointment-1"],
        });

        var putRequest = new HttpRequestMessage(HttpMethod.Put, $"api/v1/customers/{Email}")
        {
            Content = JsonContent.Create(new { FirstName = "After", LastName = "Update", Email }),
            Headers =
            {
                Authorization = new AuthenticationHeaderValue(
                    "Bearer", _tokens.CreateToken(Email, TokenFactory.CustomerRole)),
            },
        };

        var putResponse = await service.Client.SendAsync(putRequest);
        Assert.Equal(HttpStatusCode.Accepted, putResponse.StatusCode);

        var getRequest = new HttpRequestMessage(HttpMethod.Get, $"api/v1/customers/{Email}");
        getRequest.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", _tokens.CreateToken(Email, TokenFactory.CustomerRole));
        var getResponse = await service.Client.SendAsync(getRequest);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var read = await getResponse.Content.ReadFromJsonAsync<CustomerEntity>(HarnessJson.Options);

        Assert.NotNull(read);
        Assert.Equal("After", read.FirstName);
        Assert.Equal("Update", read.LastName);
        Assert.Equal(Email, read.Email);

        // Preserved by UpdateCustomerCommandHandler from the pre-existing document, not from the PUT body —
        // proving the whole-document replace round-trips fields the client never sent, not only the ones it did.
        Assert.Equal("seeded-customer-topic", read.KafkaTopic);
        Assert.Equal(["seeded-provider@example.com"], read.SubscribedProviderCollection);
        Assert.Equal(["seeded-appointment-1"], read.AppointmentCollection);
    }
}
