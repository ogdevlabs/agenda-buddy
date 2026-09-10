using System.Net;
using System.Security.Cryptography;
using System.Text;
using AgendaBuddy.Booking.Api.Payments;
using MongoDB.Driver;

namespace AgendaBuddy.IntegrationTests.Harness;

[Collection(HarnessCollection.Name)]
public class StripeWebhookTest(ServiceHostFixture<BookingAnchor> host)
    : IClassFixture<ServiceHostFixture<BookingAnchor>>
{
    private const string Secret = "whsec_integration_test";

    [Fact]
    public async Task UnsignedWebhook_IsRejected()
    {
        using var service = Start();

        var response = await service.Client.PostAsync(
            "api/v1/payments/stripe/webhook",
            new StringContent("{}", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SignedWebhookReplay_IsAcknowledgedOnceAndDeduplicated()
    {
        using var service = Start();
        var payload = """
            {
              "id": "evt_payment_replay",
              "object": "event",
              "api_version": "2022-11-15",
              "created": 1788926400,
              "data": { "object": { "id": "pi_missing", "object": "payment_intent", "status": "succeeded" } },
              "livemode": false,
              "pending_webhooks": 1,
              "request": { "id": null, "idempotency_key": null },
              "type": "payment_intent.succeeded"
            }
            """;
        var signature = Signature(payload);

        var first = await SendAsync(service, payload, signature);
        var replay = await SendAsync(service, payload, signature);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var collection = service.Database.GetCollection<StripeWebhookEvent>("stripe_webhook_events");
        Assert.Equal(1, await collection.CountDocumentsAsync(
            Builders<StripeWebhookEvent>.Filter.Eq(value => value.Id, "evt_payment_replay")));
        var recorded = await collection.Find(
            Builders<StripeWebhookEvent>.Filter.Eq(value => value.Id, "evt_payment_replay")).SingleAsync();
        Assert.NotNull(recorded.CompletedAt);
    }

    private ServiceHost Start()
    {
        return host.StartService("Production", new Dictionary<string, string>
        {
            ["Payments:Stripe:WebhookSecret"] = Secret
        });
    }

    private static string Signature(string payload)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signedPayload = $"{timestamp}.{payload}";
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), Encoding.UTF8.GetBytes(signedPayload));
        return $"t={timestamp},v1={Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    private static Task<HttpResponseMessage> SendAsync(ServiceHost service, string payload, string signature)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/payments/stripe/webhook")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.TryAddWithoutValidation("Stripe-Signature", signature);
        return service.Client.SendAsync(request);
    }
}