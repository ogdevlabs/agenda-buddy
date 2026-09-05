namespace AgendaBuddy.Customer.Api.Modules;

// TWO NEW TOP-LEVEL ROUTE GROUPS in this process (this one and NotificationModule), not children of
// /api/v1/customers -- and that is the point (ADR D-2). A message is addressed to a PERSON: a provider
// has an inbox for exactly the same reason a customer does, so a URL saying `customers` about a
// provider's inbox would assert something false and every client would have to work around it.
// Identity already hosts two unrelated groups (`/api/v1/auth` and `/device-token`), so this is a
// precedent rather than a novelty. The Customer service hosts them because it already owns the
// provider<->customer relationship these messages travel along.
//
// None of these routes are wrapped in DataResponse<T>. They call IMessageService directly, matching
// AgendaBuddy.Provider.Domain.Responses.DataResponse's own GetProviderReport precedent (a route
// deliberately left outside its service's envelope for the same reason).
public class MessageModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var messages = app.MapGroup("/api/v1/messages")
            .WithTags("MessageAPI")
            .WithOpenApi()
            .AddEndpointFilter<ProblemDetailsServiceEndpointFilter>();

        // The recipient is the caller's `sub` claim and there is NO parameter. A recipient parameter
        // would be a thing to tamper with — `MessageService.GetInboxAsync` takes one, and passing a client-supplied
        // value through would hand any authenticated caller anyone else's inbox.
        messages.MapGet("/", async Task<Results<Ok<IEnumerable<MessageEntity>>, ForbidHttpResult>> (
                ClaimsPrincipal user, IMessageService service) =>
            {
                var caller = user.FindFirstValue(ClaimTypes.NameIdentifier);
                if (caller is null) return TypedResults.Forbid();

                return TypedResults.Ok(await service.GetInboxAsync(caller));
            })
            .WithName("GetInbox")
            .RequireAuthorization();

        // ONE counterpart in the URL. `MessageService` derives thread_id by sorting both addresses, so
        // with the caller always supplying one side, a thread between two other people has no representation in this
        // URL space at all — it is unrequestable rather than merely refused.
        messages.MapGet("/thread/{counterpartEmail}",
                async Task<Results<Ok<IEnumerable<MessageEntity>>, ForbidHttpResult>> (
                    string counterpartEmail, ClaimsPrincipal user, IMessageService service) =>
                {
                    var caller = user.FindFirstValue(ClaimTypes.NameIdentifier);
                    if (caller is null) return TypedResults.Forbid();

                    return TypedResults.Ok(await service.GetThreadAsync(caller, counterpartEmail));
                })
            .WithName("GetMessageThread")
            .RequireAuthorization();

        messages.MapPost("/", async Task<Results<Created<MessageEntity>, ForbidHttpResult, BadRequest<string>>> (
                MessageRequest request,
                ClaimsPrincipal user,
                IMessageService service,
                ICustomerService customerService,
                INotificationDispatcher notificationDispatcher) =>
            {
                if (request is null || string.IsNullOrWhiteSpace(request.RecipientEmail)
                                    || string.IsNullOrWhiteSpace(request.Body))
                    return TypedResults.BadRequest("recipientEmail and body are required.");

                var caller = user.FindFirstValue(ClaimTypes.NameIdentifier);
                if (caller is null) return TypedResults.Forbid();

                if (string.Equals(caller, request.RecipientEmail, StringComparison.OrdinalIgnoreCase))
                    return TypedResults.BadRequest("You cannot message yourself.");

                // A subscription is required in BOTH directions. The provider directory is browsable by
                // design so customers can find someone to book, but browsable must not imply messageable —
                // otherwise every provider's inbox becomes a cold-outreach target and the directory becomes
                // a spam surface. Enforced here rather than only hidden in the client, because a
                // client-side-only restriction is not a restriction.
                //
                // The subscription is the relationship either way round: subscribing is what a customer
                // does to a provider, so whichever of the two is the customer is the one holding the list.
                if (!await AreRelatedAsync(customerService, caller, request.RecipientEmail))
                    return TypedResults.Forbid();

                // The sender is the caller. MessageRequest has no sender field, which is the cheapest guarantee that
                // no future refactor trusts one from the body.
                var message = new MessageEntity
                {
                    SenderEmail = caller,
                    RecipientEmail = request.RecipientEmail,
                    Body = request.Body
                };

                await service.SendMessageAsync(message);

                // The recipient is told there is something to read — an inbox nobody is pointed at is an
                // inbox nobody opens. The dispatcher decides the channels: in-app and push, deliberately not
                // email, because a conversation that emails every line is what makes people mute a product.
                // Non-fatal: the message is already stored, and answering 500 because the notification could
                // not be delivered would lose the message itself. The dispatcher already absorbs a
                // per-channel failure; this catch protects the invariant the dispatcher does not own.
                try
                {
                    var preview = request.Body.Length <= 120 ? request.Body : request.Body[..117] + "...";
                    await notificationDispatcher.DispatchAsync(new NotificationEntity(
                        recipientEmail: request.RecipientEmail,
                        subject: $"New message from {caller}",
                        body: preview,
                        type: NotificationType.MessageReceived,
                        appointmentIdentifier: string.Empty));
                }
                catch (Exception) { /* the message stands regardless */ }

                return TypedResults.Created($"/api/v1/messages/{message.Id}", message);
            })
            .WithName("SendMessage")
            .RequireAuthorization();

        // Only the RECIPIENT may mark a message read. A sender marking their own message read is meaningless, and
        // permitting it would let a sender probe whether an id exists.
        messages.MapPost("/{id}/read", async Task<Results<NoContent, ForbidHttpResult>> (
                string id, ClaimsPrincipal user, IMessageService service, IRepository<MessageEntity> repository) =>
            {
                var caller = user.FindFirstValue(ClaimTypes.NameIdentifier);
                var message = await repository.FindOneAsync(new BsonDocument("_id", new ObjectId(id)));

                // A missing message and someone else's answer identically — the same rule the notes routes follow, so
                // this cannot be used to enumerate message ids.
                try { OwnershipGuard.AssertOwner(user, message?.RecipientEmail); }
                catch (ForbiddenException) { return TypedResults.Forbid(); }

                await service.MarkReadAsync(id);
                return TypedResults.NoContent();
            })
            .WithName("MarkMessageRead")
            .RequireAuthorization();
    }
    /// <summary>
    /// True when these two addresses are on either side of a subscription. Whichever of the pair is the
    /// customer holds the list, so this looks both up and checks each in turn — the caller may be the
    /// customer or the provider, and the rule is the same relationship either way.
    /// </summary>
    private static async Task<bool> AreRelatedAsync(ICustomerService customerService, string caller, string other)
    {
        return await SubscribesToAsync(customerService, caller, other)
            || await SubscribesToAsync(customerService, other, caller);
    }

    private static async Task<bool> SubscribesToAsync(
        ICustomerService customerService, string customerEmail, string providerEmail)
    {
        var customer = await customerService.FindCustomerAsync(SupportTools<CustomerEntity>.FilterByEmail(customerEmail));
        return customer?.SubscribedProviderCollection?
            .Any(email => string.Equals(email, providerEmail, StringComparison.OrdinalIgnoreCase)) == true;
    }

}
