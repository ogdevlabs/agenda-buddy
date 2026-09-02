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
                string email,
                CancellationToken cancellationToken,
                int? days = null,
                string? service = null) =>
            {
                // NO ownership guard here, deliberately, and unlike every other calendar route.
                //
                // A customer has to see a provider's free slots in order to book one — that is the whole
                // point of the booking flow — so requiring sub == {email} made this route answer 403 to
                // every customer and the feature impossible. Authentication is still required.
                //
                // What that exposes is bounded by the response shape: this returns free START TIMES and
                // nothing else. No appointment, no counterparty, no reason, no service. Busy time is only
                // inferable as "absent from this list", which is inherent to any booking product. The
                // sibling /appointments/{email} route below stays owner-only precisely because it DOES
                // carry customer emails.
                //
                // Deliberately NOT cached (2026-08-28, agenda-buddy-326): this data is written by
                // Booking.Api, a different process with its own in-memory-only IDistributedCache
                // (AddDistributedMemoryCache -- not a shared backend like Redis). Booking has no way to
                // invalidate Calendar's copy, so caching here meant up to 5 minutes of a stale calendar
                // after any booking/cancel/confirm/complete action -- worse than the read-amplification
                // caching was meant to solve. Stale availability is also how two customers get offered
                // the same slot.
                var result = await mediator.Send(
                    new CheckCalendarAvailabilityQuery
                    {
                        Email = email,
                        Days = days ?? 30,
                        ServiceName = service
                    },
                    cancellationToken);

                // A provider who is simply fully booked is NOT "not found". This used to 404 on an empty
                // list, which the client could not distinguish from a bad provider or a dead route — so a
                // booked-out calendar surfaced as an error. 404 now means only "no such provider".
                if (result.IsFailed)
                    return TypedResults.NotFound();

                return TypedResults.Ok(DataResponse<List<DateTime>>.Ok(result.Value));
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
