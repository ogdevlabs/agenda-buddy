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
                MessageRequest request, ClaimsPrincipal user, IMessageService service) =>
            {
                if (request is null || string.IsNullOrWhiteSpace(request.RecipientEmail)
                                    || string.IsNullOrWhiteSpace(request.Body))
                    return TypedResults.BadRequest("recipientEmail and body are required.");

                var caller = user.FindFirstValue(ClaimTypes.NameIdentifier);
                if (caller is null) return TypedResults.Forbid();

                // The sender is the caller. MessageRequest has no sender field, which is the cheapest guarantee that
                // no future refactor trusts one from the body.
                var message = new MessageEntity
                {
                    SenderEmail = caller,
                    RecipientEmail = request.RecipientEmail,
                    Body = request.Body
                };

                await service.SendMessageAsync(message);
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
}
