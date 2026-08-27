using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AgendaBuddy.Library.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AgendaBuddy.IntegrationTests.Harness;

/// <summary>
/// Messages and notifications are scoped to the
/// caller, and there is no way to write a notification into somebody else's list.
/// </summary>
/// <remarks>
/// <b>Every scoping test here plants a THIRD party's records in the same database</b>, because a route that
/// returns nothing at all would satisfy "the caller sees only their own" vacuously. That is the difference
/// between asserting a filter and asserting an empty collection.
/// </remarks>
[Collection(HarnessCollection.Name)]
public class MessagingAndNotificationsTest(ServiceHostFixture<CustomerAnchor> host, CryptoSessionFixture crypto)
    : IClassFixture<ServiceHostFixture<CustomerAnchor>>
{
    private const string Caller = "caller@example.com";
    private const string Counterpart = "coach@example.com";
    private const string Outsider = "outsider@example.com";
    private const string OtherOutsider = "other-outsider@example.com";

    private readonly TokenFactory _tokens = new(crypto);

    private HttpRequestMessage Authorised(HttpMethod method, string path, string subject, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", _tokens.CreateToken(subject, TokenFactory.CustomerRole));
        if (body is not null) request.Content = JsonContent.Create(body);
        return request;
    }

    /// <summary>Two outsiders' private thread, plus a notification for one of them.</summary>
    private static async Task SeedOtherPeoplesDataAsync(ServiceHost service)
    {
        await service.Database.GetCollection<MessageEntity>("messages").InsertManyAsync(
        [
            new MessageEntity
            {
                Id = ObjectId.GenerateNewId(), SenderEmail = Outsider, RecipientEmail = OtherOutsider,
                Body = "a private conversation between two other people",
                ThreadId = $"{OtherOutsider}::{Outsider}"
            },
            new MessageEntity
            {
                Id = ObjectId.GenerateNewId(), SenderEmail = OtherOutsider, RecipientEmail = Outsider,
                Body = "and its reply", ThreadId = $"{OtherOutsider}::{Outsider}"
            }
        ]);

        await service.Database.GetCollection<NotificationEntity>("notifications").InsertOneAsync(
            new NotificationEntity
            {
                Id = ObjectId.GenerateNewId(),
                RecipientEmail = Outsider,
                Subject = "Somebody else's notification",
                Body = "not for the caller"
            });
    }

    [Theory]
    [InlineData("GET", "api/v1/messages")]
    [InlineData("POST", "api/v1/messages")]
    [InlineData("GET", "api/v1/messages/thread/coach@example.com")]
    [InlineData("POST", "api/v1/messages/000000000000000000000000/read")]
    [InlineData("GET", "api/v1/notifications")]
    [InlineData("POST", "api/v1/notifications/000000000000000000000000/read")]
    public async Task AC8_EveryMessageAndNotificationRoute_RefusesAnAnonymousCaller(string method, string path)
    {
        using var service = host.StartService("Production");

        var response = await service.Client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = JsonContent.Create(new { recipientEmail = Counterpart, body = "hello" })
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AC2_AMessageReachesTheRecipientsInboxAndTheSharedThread()
    {
        using var service = host.StartService("Production");
        await SeedOtherPeoplesDataAsync(service);

        var sent = await service.Client.SendAsync(Authorised(
            HttpMethod.Post, "api/v1/messages", Caller,
            new { recipientEmail = Counterpart, body = "Can we move to 4pm?" }));

        Assert.Equal(HttpStatusCode.Created, sent.StatusCode);
        var message = await sent.Content.ReadFromJsonAsync<MessageEntity>(HarnessJson.Options);

        // The sender came from the TOKEN — MessageRequest has no sender field at all.
        Assert.Equal(Caller, message!.SenderEmail);

        // thread_id is derived by sorting the two addresses case-insensitively, so it is the same string from
        // either side. "caller@" sorts before "coach@" ('l' < 'o'), which is worth spelling out because the
        // first draft of this assertion had the order backwards and the implementation was right.
        Assert.Equal($"{Caller}::{Counterpart}", message.ThreadId);

        // ⚠️ First time MessageEntity has ever been persisted: it was defined but never stored, because nothing
        // registered a repository for it. The read-back is the only proof its BSON mapping works.
        var recipientInbox = await service.Client.SendAsync(Authorised(HttpMethod.Get, "api/v1/messages", Counterpart));
        var inbox = await recipientInbox.Content.ReadFromJsonAsync<List<MessageEntity>>(HarnessJson.Options);
        Assert.Equal("Can we move to 4pm?", Assert.Single(inbox!).Body);

        // Both participants see the thread, and neither sees the outsiders' one.
        foreach (var (subject, counterpart) in new[] { (Caller, Counterpart), (Counterpart, Caller) })
        {
            var thread = await service.Client.SendAsync(Authorised(
                HttpMethod.Get, $"api/v1/messages/thread/{counterpart}", subject));

            var messages = await thread.Content.ReadFromJsonAsync<List<MessageEntity>>(HarnessJson.Options);
            Assert.Equal("Can we move to 4pm?", Assert.Single(messages!).Body);
        }
    }

    [Fact]
    public async Task AC11_T204_TheInboxContainsOnlyTheCallersMessages()
    {
        using var service = host.StartService("Production");
        await SeedOtherPeoplesDataAsync(service);

        var response = await service.Client.SendAsync(Authorised(HttpMethod.Get, "api/v1/messages", Caller));
        var inbox = await response.Content.ReadFromJsonAsync<List<MessageEntity>>(HarnessJson.Options);

        Assert.Empty(inbox!);

        // …and the outsiders' messages really are in the database, so the emptiness above is a filter working
        // rather than a collection that happens to be empty.
        Assert.Equal(2, await service.Database.GetCollection<MessageEntity>("messages")
            .CountDocumentsAsync(Builders<MessageEntity>.Filter.Empty));
    }

    [Fact]
    public async Task T204_AThreadBetweenTwoOtherPeopleCannotBeRequested()
    {
        // The route takes ONE counterpart; the other side is always the caller's own claim. So asking for the
        // outsiders' thread by naming one of them returns the (empty) thread between the CALLER and that
        // outsider — the private conversation has no representation in this URL space at all.
        using var service = host.StartService("Production");
        await SeedOtherPeoplesDataAsync(service);

        var response = await service.Client.SendAsync(Authorised(
            HttpMethod.Get, $"api/v1/messages/thread/{Outsider}", Caller));

        var messages = await response.Content.ReadFromJsonAsync<List<MessageEntity>>(HarnessJson.Options);
        Assert.Empty(messages!);
    }

    [Fact]
    public async Task OnlyTheRecipientCanMarkAMessageRead()
    {
        using var service = host.StartService("Production");

        var sent = await service.Client.SendAsync(Authorised(
            HttpMethod.Post, "api/v1/messages", Caller,
            new { recipientEmail = Counterpart, body = "please read this" }));
        var message = await sent.Content.ReadFromJsonAsync<MessageEntity>(HarnessJson.Options);

        // The SENDER may not: marking your own outgoing message read is meaningless, and permitting it would
        // let a sender probe which ids exist.
        var bySender = await service.Client.SendAsync(Authorised(
            HttpMethod.Post, $"api/v1/messages/{message!.Id}/read", Caller));
        Assert.Equal(HttpStatusCode.Forbidden, bySender.StatusCode);

        var byOutsider = await service.Client.SendAsync(Authorised(
            HttpMethod.Post, $"api/v1/messages/{message.Id}/read", Outsider));
        Assert.Equal(HttpStatusCode.Forbidden, byOutsider.StatusCode);

        var byRecipient = await service.Client.SendAsync(Authorised(
            HttpMethod.Post, $"api/v1/messages/{message.Id}/read", Counterpart));
        Assert.Equal(HttpStatusCode.NoContent, byRecipient.StatusCode);

        var stored = await service.Database.GetCollection<MessageEntity>("messages")
            .Find(Builders<MessageEntity>.Filter.Eq(m => m.Id, message.Id)).SingleAsync();
        Assert.True(stored.IsRead);
    }

    [Fact]
    public async Task AMissingMessageId_AndSomebodyElsesMessage_AnswerIdentically()
    {
        using var service = host.StartService("Production");
        await SeedOtherPeoplesDataAsync(service);

        var someoneElses = await service.Database.GetCollection<MessageEntity>("messages")
            .Find(Builders<MessageEntity>.Filter.Empty).FirstAsync();

        var missing = await service.Client.SendAsync(Authorised(
            HttpMethod.Post, "api/v1/messages/000000000000000000000000/read", Caller));
        var foreign = await service.Client.SendAsync(Authorised(
            HttpMethod.Post, $"api/v1/messages/{someoneElses.Id}/read", Caller));

        Assert.Equal(HttpStatusCode.Forbidden, missing.StatusCode);
        Assert.Equal(missing.StatusCode, foreign.StatusCode);
    }

    // ── Notifications ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC3_TheNotificationListIsScopedToTheCaller()
    {
        using var service = host.StartService("Production");
        await SeedOtherPeoplesDataAsync(service);

        // Planted directly, because there is deliberately no route that creates one.
        await service.Database.GetCollection<NotificationEntity>("notifications").InsertOneAsync(
            new NotificationEntity
            {
                Id = ObjectId.GenerateNewId(),
                RecipientEmail = Caller,
                Subject = "Appointment confirmed",
                Body = "Thursday 4pm"
            });

        var response = await service.Client.SendAsync(Authorised(HttpMethod.Get, "api/v1/notifications", Caller));
        var notifications = await response.Content.ReadFromJsonAsync<List<NotificationEntity>>(HarnessJson.Options);

        var mine = Assert.Single(notifications!);
        Assert.Equal("Appointment confirmed", mine.Subject);

        // The outsider's notification exists in the same collection and is not returned.
        Assert.Equal(2, await service.Database.GetCollection<NotificationEntity>("notifications")
            .CountDocumentsAsync(Builders<NotificationEntity>.Filter.Empty));
    }

    [Fact]
    public async Task T208_ThereIsNoRouteThatCreatesANotification()
    {
        // Notifications are produced by domain events, not by users: a create route would let any authenticated
        // caller write a convincing "Your appointment was cancelled" into somebody else's list. Asserted as a
        // route-table fact rather than trusted to a code review.
        using var service = host.StartService("Production");

        foreach (var path in new[] { "api/v1/notifications", "api/v1/notifications/" })
        {
            var response = await service.Client.SendAsync(Authorised(
                HttpMethod.Post, path, Caller,
                new { recipientEmail = Outsider, subject = "Your appointment was cancelled", body = "spoofed" }));

            Assert.True(
                response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
                $"POST {path} answered {response.StatusCode}. A notification-creating route must not exist "
                + "(threat T-208) — if one was added, spoofed notifications became possible.");
        }

        Assert.Equal(0, await service.Database.GetCollection<NotificationEntity>("notifications")
            .CountDocumentsAsync(Builders<NotificationEntity>.Filter.Empty));
    }

    [Fact]
    public async Task AnEmptyNotificationListIsTheNormalState_NotAnError()
    {
        // Nothing writes a notification yet: no domain event calls SendAsync (requirement 19 — storage without
        // delivery, and for now without production either). A client must render this as "nothing new", and
        // this test exists so the emptiness is a recorded expectation rather than a mystery.
        using var service = host.StartService("Production");

        var response = await service.Client.SendAsync(Authorised(HttpMethod.Get, "api/v1/notifications", Caller));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await response.Content.ReadFromJsonAsync<List<NotificationEntity>>(HarnessJson.Options))!);
    }

    [Fact]
    public async Task OnlyTheRecipientCanMarkANotificationRead()
    {
        using var service = host.StartService("Production");

        var notification = new NotificationEntity
        {
            Id = ObjectId.GenerateNewId(),
            RecipientEmail = Caller,
            Subject = "Appointment confirmed",
            Body = "Thursday 4pm"
        };
        await service.Database.GetCollection<NotificationEntity>("notifications").InsertOneAsync(notification);

        var byOutsider = await service.Client.SendAsync(Authorised(
            HttpMethod.Post, $"api/v1/notifications/{notification.Id}/read", Outsider));
        Assert.Equal(HttpStatusCode.Forbidden, byOutsider.StatusCode);

        var byOwner = await service.Client.SendAsync(Authorised(
            HttpMethod.Post, $"api/v1/notifications/{notification.Id}/read", Caller));
        Assert.Equal(HttpStatusCode.NoContent, byOwner.StatusCode);

        var stored = await service.Database.GetCollection<NotificationEntity>("notifications")
            .Find(Builders<NotificationEntity>.Filter.Eq(n => n.Id, notification.Id)).SingleAsync();
        Assert.True(stored.IsRead);
    }

    [Fact]
    public async Task AMessageWithNoRecipientOrNoBodyIsRejected()
    {
        using var service = host.StartService("Production");

        foreach (var body in new object[]
                 {
                     new { recipientEmail = "", body = "hello" },
                     new { recipientEmail = Counterpart, body = "   " }
                 })
        {
            var response = await service.Client.SendAsync(Authorised(HttpMethod.Post, "api/v1/messages", Caller, body));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
