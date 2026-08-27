using System.Net;
using System.Net.Http.Headers;
using AgendaBuddy.Library.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AgendaBuddy.IntegrationTests.Harness;

/// <summary>
/// Subscribing writes both sides of the relationship over real HTTP against a real MongoDB
/// container (ADR-053) -- <c>CustomerEntity.SubscribedProviderCollection</c> and the
/// previously-unwired <c>ProviderEntity.SubscribedCustomerCollection</c>.
/// </summary>
[Collection(HarnessCollection.Name)]
public class ProviderSubscriptionTest : IClassFixture<ServiceHostFixture<CustomerAnchor>>
{
    private const string CustomerEmail = "subscribing-customer@example.com";
    private const string ProviderEmail = "subscribed-to-provider@example.com";

    private readonly ServiceHostFixture<CustomerAnchor> _host;
    private readonly TokenFactory _tokens;

    public ProviderSubscriptionTest(ServiceHostFixture<CustomerAnchor> host, CryptoSessionFixture crypto)
    {
        _host = host;
        _tokens = new TokenFactory(crypto);
    }

    private async Task<ServiceHost> StartWithACustomerAndAProviderOnFile()
    {
        var service = _host.StartService("Production");

        await service.Database.GetCollection<CustomerEntity>("customers").InsertOneAsync(new CustomerEntity
        {
            Id = ObjectId.GenerateNewId(),
            FirstName = "Ada",
            LastName = "Lovelace",
            Email = CustomerEmail,
        });

        await service.Database.GetCollection<ProviderEntity>("providers").InsertOneAsync(new ProviderEntity
        {
            Id = ObjectId.GenerateNewId(),
            FirstName = "Grace",
            LastName = "Hopper",
            Email = ProviderEmail,
        });

        return service;
    }

    private HttpRequestMessage Request(HttpMethod method, string route, string callerSubject)
    {
        var request = new HttpRequestMessage(method, route);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokens.CreateToken(callerSubject, TokenFactory.CustomerRole));
        return request;
    }

    [Fact]
    public async Task Subscribe_WritesBothSidesOfTheRelationship()
    {
        using var service = await StartWithACustomerAndAProviderOnFile();

        var response = await service.Client.SendAsync(
            Request(HttpMethod.Post, $"api/v1/customers/{CustomerEmail}/subscriptions/{ProviderEmail}", CustomerEmail));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var customer = await service.Database.GetCollection<CustomerEntity>("customers")
            .Find(new BsonDocument("email", CustomerEmail)).FirstOrDefaultAsync();
        var provider = await service.Database.GetCollection<ProviderEntity>("providers")
            .Find(new BsonDocument("email", ProviderEmail)).FirstOrDefaultAsync();

        Assert.Contains(ProviderEmail, customer.SubscribedProviderCollection ?? []);
        Assert.Contains(CustomerEmail, provider.SubscribedCustomerCollection);
    }

    [Fact]
    public async Task Subscribe_IsIdempotent_NoDuplicateEntryOnASecondCall()
    {
        using var service = await StartWithACustomerAndAProviderOnFile();

        await service.Client.SendAsync(Request(HttpMethod.Post, $"api/v1/customers/{CustomerEmail}/subscriptions/{ProviderEmail}", CustomerEmail));
        await service.Client.SendAsync(Request(HttpMethod.Post, $"api/v1/customers/{CustomerEmail}/subscriptions/{ProviderEmail}", CustomerEmail));

        var customer = await service.Database.GetCollection<CustomerEntity>("customers")
            .Find(new BsonDocument("email", CustomerEmail)).FirstOrDefaultAsync();

        Assert.Single(customer.SubscribedProviderCollection!, ProviderEmail);
    }

    [Fact]
    public async Task Subscribe_ToANonexistentProvider_Returns404()
    {
        using var service = await StartWithACustomerAndAProviderOnFile();

        var response = await service.Client.SendAsync(
            Request(HttpMethod.Post, $"api/v1/customers/{CustomerEmail}/subscriptions/nobody@example.com", CustomerEmail));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Subscribe_AsSomeoneOtherThanTheOwningCustomer_Returns403()
    {
        using var service = await StartWithACustomerAndAProviderOnFile();

        var response = await service.Client.SendAsync(
            Request(HttpMethod.Post, $"api/v1/customers/{CustomerEmail}/subscriptions/{ProviderEmail}", "someone-else@example.com"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Unsubscribe_RemovesBothSidesOfTheRelationship()
    {
        using var service = await StartWithACustomerAndAProviderOnFile();
        await service.Client.SendAsync(Request(HttpMethod.Post, $"api/v1/customers/{CustomerEmail}/subscriptions/{ProviderEmail}", CustomerEmail));

        var response = await service.Client.SendAsync(
            Request(HttpMethod.Delete, $"api/v1/customers/{CustomerEmail}/subscriptions/{ProviderEmail}", CustomerEmail));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var customer = await service.Database.GetCollection<CustomerEntity>("customers")
            .Find(new BsonDocument("email", CustomerEmail)).FirstOrDefaultAsync();
        var provider = await service.Database.GetCollection<ProviderEntity>("providers")
            .Find(new BsonDocument("email", ProviderEmail)).FirstOrDefaultAsync();

        Assert.DoesNotContain(ProviderEmail, customer.SubscribedProviderCollection ?? []);
        Assert.DoesNotContain(CustomerEmail, provider.SubscribedCustomerCollection);
    }

    [Fact]
    public async Task Unsubscribe_FromAProviderNeverSubscribedTo_IsANoOpNotAnError()
    {
        using var service = await StartWithACustomerAndAProviderOnFile();

        var response = await service.Client.SendAsync(
            Request(HttpMethod.Delete, $"api/v1/customers/{CustomerEmail}/subscriptions/{ProviderEmail}", CustomerEmail));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task GetSubscriptions_ReturnsTheCustomersOwnList()
    {
        using var service = await StartWithACustomerAndAProviderOnFile();
        await service.Client.SendAsync(Request(HttpMethod.Post, $"api/v1/customers/{CustomerEmail}/subscriptions/{ProviderEmail}", CustomerEmail));

        var response = await service.Client.SendAsync(
            Request(HttpMethod.Get, $"api/v1/customers/{CustomerEmail}/subscriptions", CustomerEmail));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(ProviderEmail, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetSubscriptions_AsSomeoneOtherThanTheOwningCustomer_Returns403()
    {
        using var service = await StartWithACustomerAndAProviderOnFile();

        var response = await service.Client.SendAsync(
            Request(HttpMethod.Get, $"api/v1/customers/{CustomerEmail}/subscriptions", "someone-else@example.com"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
