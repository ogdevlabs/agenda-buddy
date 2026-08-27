using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AgendaBuddy.IntegrationTests.Harness;
using AgendaBuddy.Kafka;
using AgendaBuddy.Kafka.Support;
using Microsoft.Extensions.DependencyInjection;

namespace AgendaBuddy.IntegrationTests.Support;

/// <summary>
/// F-018-T10 / AC1: <see cref="KafkaClientFake"/> substitutes for the real <see cref="IKafkaClient"/> on
/// the provider-registration path, and records the topic-creation call it would otherwise send to a
/// real broker.
/// </summary>
[Collection(HarnessCollection.Name)]
public class KafkaClientFakeProviderRegistrationTest : IClassFixture<ServiceHostFixture<ProviderAnchor>>
{
    private const string Email = "kafka-fake-provider@example.com";

    private readonly ServiceHostFixture<ProviderAnchor> _host;
    private readonly TokenFactory _tokens;

    public KafkaClientFakeProviderRegistrationTest(
        ServiceHostFixture<ProviderAnchor> host, CryptoSessionFixture crypto)
    {
        _host = host;
        _tokens = new TokenFactory(crypto);
    }

    [Fact]
    public async Task GivenAKafkaClientFake_WhenAProviderRegisters_ThenTheFakeRecordsTheTopicCreationCall()
    {
        var fake = new KafkaClientFake();

        using var service = _host.StartService(
            "Production",
            configureServices: services => services.AddSingleton<IKafkaClient>(fake));

        var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/providers")
        {
            // Valid per ProviderEntity's [Required]/[EmailAddress] annotations, mirroring
            // ProviderCreationGuardTest's CreateProvider helper.
            Content = JsonContent.Create(new
            {
                FirstName = "Ada",
                LastName = $"Lovelace-{Guid.NewGuid():N}",
                Email,
            }),
        };
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _tokens.CreateToken(Email, TokenFactory.ProviderRole));

        var response = await service.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(KafkaHelper.CreateProviderTopicName(Email), Assert.Single(fake.CreatedTopics));
    }
}
