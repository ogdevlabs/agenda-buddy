using AgendaBuddy.Library.Tools;
using Microsoft.Extensions.Caching.Distributed;

namespace AgendaBuddy.Calendar.Api.Modules;

public class CalendarModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var calendar = app.MapGroup("api/v1/calendar")
            .WithTags("CalendarAPI")
            .WithOpenApi()
            .AddEndpointFilter<ProblemDetailsServiceEndpointFilter>();

        calendar.MapGet("/availability/{email}",
            async Task<Results<Ok<DataResponse<List<DateTime>>>, NotFound>> (
                IMediator mediator,
                ClaimsPrincipal user,
                string email,
                IDistributedCache cache,
                CancellationToken cancellationToken) =>
            {
                // A valid token proves the caller is SOMEBODY, not that {email} is theirs. Without this line
                // any registered user could read any provider's full appointment list, including every
                // customer email in it.
                //
                // ⚠️ DESIGN INVARIANT, NOT AN IMPLEMENTATION DETAIL: this MUST stay ABOVE the cache read. The
                // cache key is derived from {email} -- the request SUBJECT -- never the CALLER, so a cached value
                // is not necessarily one the next caller may see. Ordering is the only thing that makes it safe.
                // Reordering these lines, extracting a helper, or caching the RESPONSE instead of the DATA creates
                // a cross-tenant leak. Pinned by CalendarOwnershipTest.T006_AWarmCacheIsNotServedToADifferentPrincipal.
                //
                // No local try/catch: AgendaBuddyExceptionHandler maps ForbiddenException to 403 centrally.
                OwnershipGuard.AssertOwner(user, email);

                var key = $"availability-{email}";

                // Dispatched through the real mediator.Send with the real request CancellationToken. A Fail
                // result is mapped to null so CacheAside's "never cache a null" rule (CacheAside.cs) keeps a
                // missing provider from poisoning the cache.
                var slots = await cache.GetOrCreateAsync(key, async token =>
                {
                    var result = await mediator.Send(new CheckCalendarAvailabilityQuery { Email = email }, token);
                    return result.IsSuccess ? result.Value : null!;
                }, cancellationToken: cancellationToken);

                // Unlike the appointments route below, an empty slot list answers 404 here too -- this mirrors the
                // route's pre-existing behaviour, not a new rule.
                if (slots is null || slots.Count == 0)
                    return TypedResults.NotFound();

                return TypedResults.Ok(DataResponse<List<DateTime>>.Ok(slots));
            })
            .WithName("CheckCalendarAvailability")
            .RequireAuthorization();

        calendar.MapGet("/appointments/{email}",
            async Task<Results<Ok<DataResponse<List<AppointmentEntity>>>, NotFound>> (
                IMediator mediator,
                ClaimsPrincipal user,
                string email,
                IDistributedCache cache,
                CancellationToken cancellationToken) =>
            {
                // A valid token proves the caller is SOMEBODY, not that {email} is theirs. Without this line
                // any registered user could read any provider's full appointment list, including every
                // customer email in it.
                //
                // ⚠️ DESIGN INVARIANT, NOT AN IMPLEMENTATION DETAIL: this MUST stay ABOVE the cache read. The
                // cache key is derived from {email} -- the request SUBJECT -- never the CALLER, so a cached value
                // is not necessarily one the next caller may see. Ordering is the only thing that makes it safe.
                // Reordering these lines, extracting a helper, or caching the RESPONSE instead of the DATA creates
                // a cross-tenant leak. Pinned by CalendarOwnershipTest.T006_AWarmCacheIsNotServedToADifferentPrincipal.
                //
                // No local try/catch: AgendaBuddyExceptionHandler maps ForbiddenException to 403 centrally.
                OwnershipGuard.AssertOwner(user, email);

                var key = $"appointments-{email}";

                var appointmentEntities = await cache.GetOrCreateAsync(key, async token =>
                {
                    var result = await mediator.Send(new CheckCalendarAppointmentsQuery { Email = email }, token);
                    return result.IsSuccess ? result.Value : null!;
                }, cancellationToken: cancellationToken);

                // Unlike availability above, an empty (but non-null) appointment list is a valid 200 -- a provider
                // with no appointments is not "not found". This mirrors the route's pre-existing behaviour,
                // not a new rule.
                if (appointmentEntities is not null) return TypedResults.Ok(DataResponse<List<AppointmentEntity>>.Ok(appointmentEntities));

                return TypedResults.NotFound();
            })
            .WithName("CheckCalendarAppointments")
            .RequireAuthorization();
    }
}
