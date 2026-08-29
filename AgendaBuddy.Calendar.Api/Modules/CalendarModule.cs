using AgendaBuddy.Library.Tools;

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
                CancellationToken cancellationToken) =>
            {
                // A valid token proves the caller is SOMEBODY, not that {email} is theirs. Without this line
                // any registered user could read any provider's full appointment list, including every
                // customer email in it.
                //
                // No local try/catch: AgendaBuddyExceptionHandler maps ForbiddenException to 403 centrally.
                OwnershipGuard.AssertOwner(user, email);

                // Deliberately NOT cached (2026-08-28, agenda-buddy-326): this data is written by
                // Booking.Api, a different process with its own in-memory-only IDistributedCache
                // (AddDistributedMemoryCache -- not a shared backend like Redis). Booking has no way to
                // invalidate Calendar's copy, so caching here meant up to 5 minutes of a stale calendar
                // after any booking/cancel/confirm/complete action -- worse than the read-amplification
                // caching was meant to solve. Revisit once there is a real shared cache or an
                // event-driven invalidation path (e.g. Booking publishing to Kafka, Calendar evicting on
                // receipt).
                var result = await mediator.Send(new CheckCalendarAvailabilityQuery { Email = email }, cancellationToken);
                var slots = result.IsSuccess ? result.Value : null;

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
                CancellationToken cancellationToken) =>
            {
                // A valid token proves the caller is SOMEBODY, not that {email} is theirs. Without this line
                // any registered user could read any provider's full appointment list, including every
                // customer email in it.
                //
                // No local try/catch: AgendaBuddyExceptionHandler maps ForbiddenException to 403 centrally.
                OwnershipGuard.AssertOwner(user, email);

                // Deliberately NOT cached -- see the remark on the availability route above; same
                // cross-process staleness gap, and this is the exact data the mobile Calendar tab
                // renders, so a stale read here is directly user-visible.
                var result = await mediator.Send(new CheckCalendarAppointmentsQuery { Email = email }, cancellationToken);
                var appointmentEntities = result.IsSuccess ? result.Value : null;

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
