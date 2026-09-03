using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AgendaBuddy.IntegrationTests.Harness;
using AgendaBuddy.Library.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AgendaBuddy.IntegrationTests.Persistence;

/// <summary>
/// <c>PUT /api/v1/customers/{email}</c> followed by <c>GET /api/v1/customers/{email}</c>
/// proves <see cref="CustomerEntity"/>'s <c>[BsonElement]</c> mapping round-trips through a real write and
/// a real read.
/// </summary>
/// <remarks>
/// <b>Not <c>POST /api/v1/customers</c>.</b> A seeded starting document plus a real <c>PUT</c> is what
/// exercises the update path end to end, including the fields the client never sends.
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

        // The response is wrapped in DataResponse<T> (ADR-049) -- the object is under a "data"
        // property, not the response root. Parsed field-by-field rather than re-deserialised into
        // CustomerEntity at "data", matching the other persistence tests.
        using var body = JsonDocument.Parse(await getResponse.Content.ReadAsStringAsync());
        var read = body.RootElement.GetProperty("data");

        Assert.Equal("After", read.GetProperty("firstName").GetString());
        Assert.Equal("Update", read.GetProperty("lastName").GetString());
        Assert.Equal(Email, read.GetProperty("email").GetString());

        // Preserved by UpdateCustomerCommandHandler from the pre-existing document, not from the PUT body —
        // proving the whole-document replace round-trips fields the client never sent, not only the ones it did.
        Assert.Equal(["seeded-provider@example.com"], read.GetProperty("subscribedProviderCollection").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(["seeded-appointment-1"], read.GetProperty("appointmentCollection").EnumerateArray().Select(e => e.GetString()));
    }
}
