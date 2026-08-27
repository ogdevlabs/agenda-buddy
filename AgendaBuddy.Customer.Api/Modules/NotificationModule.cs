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
        notifications.MapGet("/", async Task<Results<Ok<IEnumerable<NotificationEntity>>, ForbidHttpResult>> (
                ClaimsPrincipal user, INotificationService service) =>
            {
                var caller = user.FindFirstValue(ClaimTypes.NameIdentifier);
                if (caller is null) return TypedResults.Forbid();

                return TypedResults.Ok(await service.GetForRecipientAsync(caller));
            })
            .WithName("GetNotifications")
            .RequireAuthorization();

        notifications.MapPost("/{id}/read", async Task<Results<NoContent, ForbidHttpResult>> (
                string id, ClaimsPrincipal user, INotificationService service, IRepository<NotificationEntity> repository) =>
            {
                var notification = await repository.FindOneAsync(new BsonDocument("_id", new ObjectId(id)));

                try { OwnershipGuard.AssertOwner(user, notification?.RecipientEmail); }
                catch (ForbiddenException) { return TypedResults.Forbid(); }

                await service.MarkReadAsync(id);
                return TypedResults.NoContent();
            })
            .WithName("MarkNotificationRead")
            .RequireAuthorization();
    }
}
