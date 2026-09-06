using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AgendaBuddy.Library.Services;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace AgendaBuddy.Library.Tests.Services;

/// <summary>
/// FCM HTTP v1. The interesting part is not the POST — it is the OAuth2 assertion, because a malformed one
/// fails at Google with a status code and no explanation of which claim was wrong.
/// </summary>
public class FcmPushSenderTest
{
    private const string ProjectId = "agenda-me-test";
    private const string ClientEmail = "push@agenda-me-test.iam.gserviceaccount.com";

    private static readonly string PrivateKeyPem = CreatePrivateKeyPem();

    private static string CreatePrivateKeyPem()
    {
        using var rsa = RSA.Create(2048);
        return rsa.ExportPkcs8PrivateKeyPem();
    }

    private static string ServiceAccountJson(
        string? clientEmail = ClientEmail, string? privateKey = null, string? tokenUri = null)
    {
        var fields = new Dictionary<string, object?>
        {
            ["type"] = "service_account",
            ["project_id"] = ProjectId,
            ["client_email"] = clientEmail,
            ["private_key"] = privateKey ?? PrivateKeyPem
        };
        if (tokenUri is not null) fields["token_uri"] = tokenUri;

        return JsonSerializer.Serialize(fields);
    }

    private static FcmPushSender Create(RecordingHandler handler, string? serviceAccountJson = null)
    {
        var client = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(FcmPushSender.HttpClientName)).Returns(client);

        return new FcmPushSender(
            factory.Object,
            Options.Create(new PushOptions
            {
                FirebaseProjectId = ProjectId,
                ServiceAccountJson = serviceAccountJson ?? ServiceAccountJson()
            }));
    }

    // ── The happy path ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Send_ExchangesTheAssertionForATokenThenPostsToTheProjectsSendEndpoint()
    {
        var handler = new RecordingHandler();
        var sender = Create(handler);

        Assert.True(await sender.SendAsync(
            "device-token", "New appointment request", "Someone requested a session.",
            new Dictionary<string, string> { ["appointmentIdentifier"] = "appt-42" }));

        Assert.Equal(2, handler.Requests.Count);

        // The token exchange first, as a form post to Google.
        Assert.Equal("https://oauth2.googleapis.com/token", handler.Requests[0].Uri);
        Assert.Contains("grant_type=urn%3Aietf%3Aparams%3Aoauth%3Agrant-type%3Ajwt-bearer", handler.Requests[0].Body);

        // Then the send, bearing the token Google returned.
        Assert.Equal(
            $"https://fcm.googleapis.com/v1/projects/{ProjectId}/messages:send", handler.Requests[1].Uri);
        Assert.Equal("Bearer test-access-token", handler.Requests[1].Authorization);
    }

    /// <summary>
    /// The message shape FCM v1 requires: everything nested under <c>message</c>, the device token as
    /// <c>token</c>, and the visible text under <c>notification</c>.
    /// </summary>
    [Fact]
    public async Task Send_BuildsTheV1MessageEnvelope()
    {
        var handler = new RecordingHandler();
        var sender = Create(handler);

        await sender.SendAsync("device-token-abc", "Appointment confirmed", "Friday at 2pm");

        using var document = JsonDocument.Parse(handler.Requests[1].Body);
        var message = document.RootElement.GetProperty("message");

        Assert.Equal("device-token-abc", message.GetProperty("token").GetString());
        Assert.Equal("Appointment confirmed", message.GetProperty("notification").GetProperty("title").GetString());
        Assert.Equal("Friday at 2pm", message.GetProperty("notification").GetProperty("body").GetString());
    }

    /// <summary>
    /// The data payload is what lets a tapped notification open the appointment. Its key has to match the
    /// client's <c>PushNotificationService.AppointmentIdentifierKey</c>.
    /// </summary>
    [Fact]
    public async Task Send_CarriesTheDataPayloadWhenThereIsOne()
    {
        var handler = new RecordingHandler();
        var sender = Create(handler);

        await sender.SendAsync("device-token", "Title", "Body",
            new Dictionary<string, string> { ["appointmentIdentifier"] = "appt-42" });

        using var document = JsonDocument.Parse(handler.Requests[1].Body);
        Assert.Equal("appt-42", document.RootElement
            .GetProperty("message").GetProperty("data").GetProperty("appointmentIdentifier").GetString());
    }

    // ── Delivery instructions ───────────────────────────────────────────────────────────────────────
    // FCM's defaults are wrong for this product in three ways that all present as "push does not work": a
    // normal-priority message can sit in Doze for hours, a soundless notification is drawn silently, and a
    // message naming no Android channel lands on the SDK's own "Miscellaneous" one.

    /// <summary>
    /// High priority on both platforms. <c>apns-priority: 10</c> is valid here precisely because every message
    /// carries a visible alert — a background-only push would have to use 5, and 10 with no alert is rejected.
    /// </summary>
    [Fact]
    public async Task Send_AsksForImmediateDeliveryOnBothPlatforms()
    {
        var handler = new RecordingHandler();
        var sender = Create(handler);

        await sender.SendAsync("device-token", "New appointment request", "Friday at 2pm");

        using var document = JsonDocument.Parse(handler.Requests[1].Body);
        var message = document.RootElement.GetProperty("message");

        Assert.Equal("high", message.GetProperty("android").GetProperty("priority").GetString());
        Assert.Equal("10", message
            .GetProperty("apns").GetProperty("headers").GetProperty("apns-priority").GetString());
    }

    // A notification with no sound is indistinguishable from one that never arrived, unless the screen is watched.
    [Fact]
    public async Task Send_AsksForASoundOnBothPlatforms()
    {
        var handler = new RecordingHandler();
        var sender = Create(handler);

        await sender.SendAsync("device-token", "Title", "Body");

        using var document = JsonDocument.Parse(handler.Requests[1].Body);
        var message = document.RootElement.GetProperty("message");

        Assert.True(message
            .GetProperty("android").GetProperty("notification").GetProperty("default_sound").GetBoolean());
        Assert.Equal("default", message
            .GetProperty("apns").GetProperty("payload").GetProperty("aps").GetProperty("sound").GetString());
    }

    /// <summary>
    /// The channel has to be the one the client creates and declares in its manifest. Naming a channel the app
    /// has not created is silent: Android posts to the SDK's auto-created channel instead, every notification
    /// still arrives, and the app's own channel settings do nothing.
    /// </summary>
    [Fact]
    public async Task Send_NamesTheAndroidChannelTheClientDeclares()
    {
        var handler = new RecordingHandler();
        var sender = Create(handler);

        await sender.SendAsync("device-token", "Title", "Body");

        using var document = JsonDocument.Parse(handler.Requests[1].Body);

        Assert.Equal(PushOptions.AndroidChannelId, document.RootElement
            .GetProperty("message").GetProperty("android").GetProperty("notification")
            .GetProperty("channel_id").GetString());
    }

    /// <summary>
    /// The field names are FCM's own, and the serializer must not rewrite them: the send would answer 400 for an
    /// unknown field, and the delivery instructions above would be silently absent from a message that still
    /// arrives.
    /// </summary>
    [Fact]
    public async Task Send_KeepsFcmsOwnFieldNamesRatherThanRecasingThem()
    {
        var handler = new RecordingHandler();
        var sender = Create(handler);

        await sender.SendAsync("device-token", "Title", "Body");

        Assert.Contains("\"channel_id\"", handler.Requests[1].Body);
        Assert.Contains("\"default_sound\"", handler.Requests[1].Body);
        Assert.Contains("\"apns-priority\"", handler.Requests[1].Body);
    }

    // FCM rejects `data: null`, so a notification with no appointment must omit the key entirely.
    [Fact]
    public async Task Send_OmitsTheDataKeyEntirelyWhenThereIsNoPayload()
    {
        var handler = new RecordingHandler();
        var sender = Create(handler);

        await sender.SendAsync("device-token", "New message from a@b.dev", "Are we still on?");

        using var document = JsonDocument.Parse(handler.Requests[1].Body);
        Assert.False(document.RootElement.GetProperty("message").TryGetProperty("data", out _));
    }

    // ── The assertion ───────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A real RS256 signature over the real signing input, verifiable with the public half of the key — not
    /// merely "three dot-separated segments". A wrong signature is indistinguishable from a wrong claim at the
    /// far end, so it is worth checking here.
    /// </summary>
    [Fact]
    public async Task TheAssertionIsAnRs256JwtSignedWithTheServiceAccountKey()
    {
        var handler = new RecordingHandler();
        var sender = Create(handler);
        await sender.SendAsync("device-token", "Title", "Body");

        var assertion = ExtractAssertion(handler.Requests[0].Body);
        var segments = assertion.Split('.');
        Assert.Equal(3, segments.Length);

        using var rsa = RSA.Create();
        rsa.ImportFromPem(PrivateKeyPem);
        Assert.True(rsa.VerifyData(
            Encoding.ASCII.GetBytes($"{segments[0]}.{segments[1]}"),
            FromBase64Url(segments[2]),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1));
    }

    [Fact]
    public async Task TheAssertionCarriesTheClaimsGoogleRequires()
    {
        var handler = new RecordingHandler();
        var sender = Create(handler);
        await sender.SendAsync("device-token", "Title", "Body");

        var segments = ExtractAssertion(handler.Requests[0].Body).Split('.');

        using var header = JsonDocument.Parse(FromBase64Url(segments[0]));
        Assert.Equal("RS256", header.RootElement.GetProperty("alg").GetString());

        using var claims = JsonDocument.Parse(FromBase64Url(segments[1]));
        var root = claims.RootElement;
        Assert.Equal(ClientEmail, root.GetProperty("iss").GetString());
        Assert.Equal("https://www.googleapis.com/auth/firebase.messaging", root.GetProperty("scope").GetString());
        Assert.Equal("https://oauth2.googleapis.com/token", root.GetProperty("aud").GetString());

        // One hour is Google's maximum for an assertion; anything longer is rejected outright.
        var lifetime = root.GetProperty("exp").GetInt64() - root.GetProperty("iat").GetInt64();
        Assert.Equal(3600, lifetime);
    }

    // Base64url per RFC 7515: URL-safe alphabet and no padding. Plain base64 would be rejected.
    [Fact]
    public async Task TheAssertionIsBase64UrlEncodedWithoutPadding()
    {
        var handler = new RecordingHandler();
        var sender = Create(handler);
        await sender.SendAsync("device-token", "Title", "Body");

        var assertion = ExtractAssertion(handler.Requests[0].Body);

        Assert.DoesNotContain('+', assertion);
        Assert.DoesNotContain('/', assertion);
        Assert.DoesNotContain('=', assertion);
    }

    [Fact]
    public async Task AServiceAccountWithItsOwnTokenUri_IsHonoured()
    {
        var handler = new RecordingHandler();
        var sender = Create(handler, ServiceAccountJson(tokenUri: "https://oauth2.example.test/token"));

        await sender.SendAsync("device-token", "Title", "Body");

        Assert.Equal("https://oauth2.example.test/token", handler.Requests[0].Uri);
    }

    // ── Token caching ───────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// One token across sends. Google rate-limits the exchange, so minting one per notification would fail
    /// exactly when a burst of them matters most.
    /// </summary>
    [Fact]
    public async Task TheAccessTokenIsMintedOnceAndReusedAcrossSends()
    {
        var handler = new RecordingHandler();
        var sender = Create(handler);

        await sender.SendAsync("device-token", "One", "Body");
        await sender.SendAsync("device-token", "Two", "Body");
        await sender.SendAsync("device-token", "Three", "Body");

        Assert.Equal(1, handler.TokenRequestCount);
        Assert.Equal(3, handler.SendRequestCount);
    }

    // A near-expiry token is renewed rather than used, so it cannot lapse mid-flight.
    [Fact]
    public async Task ATokenAboutToExpireIsRenewed()
    {
        var handler = new RecordingHandler { TokenLifetimeSeconds = 30 };
        var sender = Create(handler);

        await sender.SendAsync("device-token", "One", "Body");
        await sender.SendAsync("device-token", "Two", "Body");

        Assert.Equal(2, handler.TokenRequestCount);
    }

    /// <summary>
    /// A burst mints one token, not one each — the re-check inside the lock is what makes that true.
    /// </summary>
    [Fact]
    public async Task ConcurrentSendsMintOneTokenBetweenThem()
    {
        var handler = new RecordingHandler { TokenDelay = TimeSpan.FromMilliseconds(50) };
        var sender = Create(handler);

        await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(i => sender.SendAsync("device-token", $"Notification {i}", "Body")));

        Assert.Equal(1, handler.TokenRequestCount);
        Assert.Equal(8, handler.SendRequestCount);
    }

    // ── Failure ─────────────────────────────────────────────────────────────────────────────────────

    // Swallowed by contract: a delivery failure must never fail the appointment that triggered it.
    [Fact]
    public async Task AFailedTokenExchange_ReportsFailureWithoutThrowingOrPosting()
    {
        var handler = new RecordingHandler { TokenStatus = HttpStatusCode.Unauthorized };
        var sender = Create(handler);

        Assert.False(await sender.SendAsync("device-token", "Title", "Body"));
        Assert.Equal(0, handler.SendRequestCount);
    }

    [Fact]
    public async Task AFailedSend_ReportsFailureWithoutThrowing()
    {
        var handler = new RecordingHandler { SendStatus = HttpStatusCode.BadRequest };
        var sender = Create(handler);

        Assert.False(await sender.SendAsync("device-token", "Title", "Body"));
    }

    /// <summary>
    /// 404 is FCM's answer for a token it no longer recognises — the app was uninstalled, or the token rotated.
    /// Still a failure, but a different one from "push is broken".
    /// </summary>
    [Fact]
    public async Task AnUnregisteredDeviceToken_ReportsFailureWithoutThrowing()
    {
        var handler = new RecordingHandler { SendStatus = HttpStatusCode.NotFound };
        var sender = Create(handler);

        Assert.False(await sender.SendAsync("stale-token", "Title", "Body"));
    }

    [Fact]
    public async Task AnUnreachableProvider_ReportsFailureWithoutThrowing()
    {
        var handler = new RecordingHandler { Throw = new HttpRequestException("dns failure") };
        var sender = Create(handler);

        Assert.False(await sender.SendAsync("device-token", "Title", "Body"));
    }

    [Fact]
    public async Task AnEmptyDeviceToken_SendsNothing()
    {
        var handler = new RecordingHandler();
        var sender = Create(handler);

        Assert.False(await sender.SendAsync("", "Title", "Body"));
        Assert.Empty(handler.Requests);
    }

    /// <summary>
    /// A malformed credential throws at construction rather than at send time. A misconfiguration is cheapest
    /// to notice at startup, and silently degrading to "delivered nothing" would hide a typo forever.
    /// </summary>
    [Theory]
    [InlineData("{}")]
    [InlineData("""{"client_email":"a@b.test"}""")]
    [InlineData("""{"private_key":"-----BEGIN PRIVATE KEY-----"}""")]
    public void AServiceAccountJsonMissingItsKeyFields_FailsAtConstruction(string json)
    {
        var factory = new Mock<IHttpClientFactory>();

        var exception = Assert.Throws<InvalidOperationException>(() => new FcmPushSender(
            factory.Object,
            Options.Create(new PushOptions { FirebaseProjectId = ProjectId, ServiceAccountJson = json })));

        // Names the key and how to get one, so the failure is actionable.
        Assert.Contains("ServiceAccountJson", exception.Message);
        Assert.Contains("Firebase Console", exception.Message);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────────────

    private static string ExtractAssertion(string formBody)
    {
        foreach (var pair in formBody.Split('&'))
        {
            var parts = pair.Split('=', 2);
            if (parts[0] == "assertion") return Uri.UnescapeDataString(parts[1]);
        }

        throw new InvalidOperationException($"No assertion in '{formBody}'.");
    }

    private static byte[] FromBase64Url(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(padded.PadRight((padded.Length + 3) / 4 * 4, '='));
    }

    private sealed record CapturedRequest(string Uri, string Body, string? Authorization);

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<CapturedRequest> Requests { get; } = [];
        public HttpStatusCode TokenStatus { get; init; } = HttpStatusCode.OK;
        public HttpStatusCode SendStatus { get; init; } = HttpStatusCode.OK;
        public int TokenLifetimeSeconds { get; init; } = 3600;
        public TimeSpan TokenDelay { get; init; } = TimeSpan.Zero;
        public Exception? Throw { get; init; }

        private int _tokenRequestCount;
        public int TokenRequestCount => _tokenRequestCount;
        public int SendRequestCount => Requests.Count(r => r.Uri.Contains("fcm.googleapis.com"));

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Throw is not null) throw Throw;

            var uri = request.RequestUri!.ToString();
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            lock (Requests)
            {
                Requests.Add(new CapturedRequest(uri, body, request.Headers.Authorization?.ToString()));
            }

            if (uri.Contains("/token"))
            {
                Interlocked.Increment(ref _tokenRequestCount);
                if (TokenDelay > TimeSpan.Zero) await Task.Delay(TokenDelay, cancellationToken);

                if (TokenStatus != HttpStatusCode.OK) return new HttpResponseMessage(TokenStatus);

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        $$"""{"access_token":"test-access-token","expires_in":{{TokenLifetimeSeconds}},"token_type":"Bearer"}""",
                        Encoding.UTF8,
                        "application/json")
                };
            }

            return new HttpResponseMessage(SendStatus)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };
        }
    }
}
