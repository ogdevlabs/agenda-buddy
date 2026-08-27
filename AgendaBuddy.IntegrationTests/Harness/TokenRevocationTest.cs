using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using AgendaBuddy.Library.Entities;
using MongoDB.Bson;

namespace AgendaBuddy.IntegrationTests.Harness;

/// <summary>
/// Proves the denylist wired into <c>AuthenticationExtensions</c> (F-023) actually rejects a
/// revoked token over real HTTP, against a real MongoDB-backed <c>ITokenRevocationStore</c> — not
/// just that the code compiles.
/// </summary>
[Collection(HarnessCollection.Name)]
public class TokenRevocationTest : IClassFixture<ServiceHostFixture<CalendarAnchor>>
{
    private const string Owner = "revocation-owner@example.com";

    private readonly ServiceHostFixture<CalendarAnchor> _host;
    private readonly TokenFactory _tokens;

    public TokenRevocationTest(ServiceHostFixture<CalendarAnchor> host, CryptoSessionFixture crypto)
    {
        _host = host;
        _tokens = new TokenFactory(crypto);
    }

    private async Task<ServiceHost> StartWithOwnerProviderAsync()
    {
        var service = _host.StartService("Production");
        await service.Database.GetCollection<ProviderEntity>("providers").InsertOneAsync(new ProviderEntity
        {
            Id = ObjectId.GenerateNewId(),
            FirstName = "Ada",
            LastName = "Lovelace",
            Email = Owner,
            AppointmentEntities = [],
        });
        return service;
    }

    private static HttpRequestMessage Read(string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"api/v1/calendar/availability/{Owner}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private static string JtiOf(string accessToken) =>
        new JwtSecurityTokenHandler().ReadJwtToken(accessToken)
            .Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;

    [Fact]
    public async Task ARevokedTokensJtiIsRejectedOnItsNextRequest()
    {
        using var service = await StartWithOwnerProviderAsync();
        var accessToken = _tokens.CreateToken(Owner, TokenFactory.ProviderRole);

        // Baseline: the token is good before anything is revoked.
        var before = await service.Client.SendAsync(Read(accessToken));
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);

        // Simulates what IdentityService.LogoutAsync writes on logout — this test targets the
        // AuthenticationExtensions.OnTokenValidated check directly rather than round-tripping
        // through Identity's own host, the same seed-the-collection-directly approach LogoutTest
        // uses for the refresh-token side of the same feature.
        await service.Database.GetCollection<BsonDocument>("revoked_tokens").InsertOneAsync(new BsonDocument
        {
            { "_id", JtiOf(accessToken) },
            { "expires_at", DateTime.UtcNow.AddMinutes(30) },
        });

        var after = await service.Client.SendAsync(Read(accessToken));
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    [Fact]
    public async Task ADifferentTokensJtiIsUnaffectedByAnUnrelatedRevocation()
    {
        using var service = await StartWithOwnerProviderAsync();
        var accessToken = _tokens.CreateToken(Owner, TokenFactory.ProviderRole);

        await service.Database.GetCollection<BsonDocument>("revoked_tokens").InsertOneAsync(new BsonDocument
        {
            { "_id", Guid.NewGuid().ToString() },
            { "expires_at", DateTime.UtcNow.AddMinutes(30) },
        });

        var response = await service.Client.SendAsync(Read(accessToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
