namespace AgendaBuddy.Customer.Api.Modules;

public class NotificationModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var notifications = app.MapGroup("/api/v1/notifications")
            .WithTags("NotificationAPI")
            .WithOpenApi()
            .AddEndpointFilter<ProblemDetailsServiceEndpointFilter>();

        // ⚠️ THERE IS DELIBERATELY NO ROUTE THAT CREATES A NOTIFICATION. Notifications are produced by
        // domain events, not by users: a create route would let any authenticated caller write a convincing "Your
        // appointment was cancelled" into somebody else's list. `NotificationService.SendAsync` stays reachable
        // in-process to whatever writes one.
        //
        // Newest first and bounded, both applied in the database. `limit` is clamped rather than rejected
        // (ADR-023) — an out-of-range page size is a client bug that must not cost the user their inbox.
        //
        // The clamp lives in NotificationService alone and is deliberately NOT repeated here. A second
        // Math.Clamp(limit, 1, Max) at this boundary looked like defence in depth and was a bug: it turned
        // limit=0 into 1 before the service could read it as "unspecified, use the default", so a client
        // sending 0 got one notification instead of a page. One rule, one place.
        notifications.MapGet("/", async Task<Results<Ok<IEnumerable<NotificationEntity>>, ForbidHttpResult>> (
                ClaimsPrincipal user,
                INotificationService service,
                int? limit,
                bool? unreadOnly) =>
            {
                var caller = user.FindFirstValue(ClaimTypes.NameIdentifier);
                if (caller is null) return TypedResults.Forbid();

                return TypedResults.Ok(await service.GetForRecipientAsync(
                    caller, limit ?? NotificationService.DefaultPageSize, unreadOnly ?? false));
            })
            .WithName("GetNotifications")
            .RequireAuthorization();

        // Counted in the database rather than derived from the list, so the badge does not need the list.
        // A client that has to fetch every notification to learn there are none unread is a client that
        // fetches every notification on every screen.
        notifications.MapGet("/unread-count", async Task<Results<Ok<UnreadCountResponse>, ForbidHttpResult>> (
                ClaimsPrincipal user, INotificationService service) =>
            {
                var caller = user.FindFirstValue(ClaimTypes.NameIdentifier);
                if (caller is null) return TypedResults.Forbid();

                return TypedResults.Ok(new UnreadCountResponse(await service.CountUnreadAsync(caller)));
            })
            .WithName("GetUnreadNotificationCount")
            .RequireAuthorization();

        notifications.MapPost("/{id}/read", async Task<Results<NoContent, ForbidHttpResult>> (
                string id, ClaimsPrincipal user, INotificationService service, IRepository<NotificationEntity> repository) =>
            {
                if (!ObjectId.TryParse(id, out var objectId)) return TypedResults.NoContent();

                var notification = await repository.FindOneAsync(new BsonDocument("_id", objectId));

                try { OwnershipGuard.AssertOwner(user, notification?.RecipientEmail); }
                catch (ForbiddenException) { return TypedResults.Forbid(); }

                await service.MarkReadAsync(id);
                return TypedResults.NoContent();
            })
            .WithName("MarkNotificationRead")
            .WithSummary("Marks one notification read. Idempotent: already-read writes nothing.")
            .RequireAuthorization();

        // Scoped by the CALLER'S OWN claim, never by a body or a route parameter — there is no address here
        // for a caller to substitute, so this cannot clear somebody else's inbox. `read-all`, not `read`,
        // so it can never collide with the `{id}/read` route above.
        notifications.MapPost("/read-all", async Task<Results<Ok<MarkAllReadResponse>, ForbidHttpResult>> (
                ClaimsPrincipal user, INotificationService service) =>
            {
                var caller = user.FindFirstValue(ClaimTypes.NameIdentifier);
                if (caller is null) return TypedResults.Forbid();

                return TypedResults.Ok(new MarkAllReadResponse(await service.MarkAllReadAsync(caller)));
            })
            .WithName("MarkAllNotificationsRead")
            .WithSummary("Marks every unread notification read. Returns how many changed; zero is a success.")
            .RequireAuthorization();
    }
}

/// <summary>How many of the caller's notifications are unread.</summary>
/// <remarks>
/// An object rather than a bare number: a bare JSON scalar is a valid body that no client can extend, and
/// this one will grow (a per-category count is the obvious next ask).
/// </remarks>
public record UnreadCountResponse(long UnreadCount);

/// <summary>How many notifications a bulk mark-read actually changed.</summary>
public record MarkAllReadResponse(long MarkedRead);
