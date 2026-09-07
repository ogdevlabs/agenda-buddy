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

        // ── Time off ──────────────────────────────────────────────────────────────────────────────────
        //
        // A block is a first-class record on its own collection, replacing the whole-day fake appointment
        // (an AppointmentEntity with day_off = true, an invented empty customer email, no time range and no
        // reason). Every route here is OWNER-ONLY, and that is a stronger requirement than it looks: a
        // block's `reason` is the provider's private note. The customer-facing availability route already
        // reflects blocks -- as ABSENCE, which is all a customer needs and all they get.

        calendar.MapGet("/blocks/{email}",
            async Task<Results<Ok<DataResponse<List<CalendarBlockEntity>>>, NotFound>> (
                IMediator mediator,
                ClaimsPrincipal user,
                string email,
                CancellationToken cancellationToken) =>
            {
                // No local try/catch: AgendaBuddyExceptionHandler maps ForbiddenException to 403 centrally.
                OwnershipGuard.AssertOwner(user, email);

                var result = await mediator.Send(
                    new GetCalendarBlocksQuery { EmailProvider = email }, cancellationToken);

                // A provider with no time off recorded is not "not found" -- an empty list is the answer.
                return result.IsSuccess
                    ? TypedResults.Ok(DataResponse<List<CalendarBlockEntity>>.Ok(result.Value))
                    : TypedResults.NotFound();
            })
            .WithName("GetCalendarBlocks")
            .RequireAuthorization();

        // What a proposed range would strand, read BEFORE creating a block, so the cost is visible in
        // advance rather than discovered afterwards. Owner-only: it returns whole appointments, which carry
        // the counterparty's email.
        calendar.MapGet("/blocks/{email}/conflicts",
            async Task<Results<Ok<DataResponse<List<AppointmentEntity>>>, BadRequest<DataResponse<List<AppointmentEntity>>>>> (
                IMediator mediator,
                ClaimsPrincipal user,
                string email,
                DateTime startUtc,
                DateTime endUtc,
                CancellationToken cancellationToken) =>
            {
                OwnershipGuard.AssertOwner(user, email);

                var result = await mediator.Send(
                    new GetCalendarBlockConflictsQuery
                    {
                        EmailProvider = email,
                        StartUtc = startUtc,
                        EndUtc = endUtc
                    },
                    cancellationToken);

                return result.IsSuccess
                    ? TypedResults.Ok(DataResponse<List<AppointmentEntity>>.Ok(result.Value))
                    : TypedResults.BadRequest(DataResponse<List<AppointmentEntity>>.Fail(
                        result.Errors.Select(error => error.Message)));
            })
            .WithName("GetCalendarBlockConflicts")
            .RequireAuthorization();

        calendar.MapPost("/blocks/{email}",
            async Task<Results<ValidationProblem, Created<DataResponse<CalendarBlockEntity>>, Conflict<DataResponse<CalendarBlockEntity>>, BadRequest<DataResponse<CalendarBlockEntity>>>> (
                IMediator mediator,
                ClaimsPrincipal user,
                string email,
                CalendarBlockRequest request,
                CancellationToken cancellationToken) =>
            {
                OwnershipGuard.AssertOwner(user, email);

                if (!MiniValidator.TryValidate(request, out var errors))
                    return TypedResults.ValidationProblem(errors);

                var result = await mediator.Send(
                    new BlockCalendarCommand
                    {
                        EmailProvider = email,
                        StartUtc = request.StartUtc,
                        EndUtc = request.EndUtc,
                        Reason = request.Reason,
                        Force = request.Force
                    },
                    cancellationToken);

                if (result.IsSuccess)
                {
                    return TypedResults.Created(
                        $"/api/v1/calendar/blocks/{email}",
                        DataResponse<CalendarBlockEntity>.Ok(result.Value));
                }

                var messages = result.Errors.Select(error => error.Message).ToList();

                // 409 for "sessions are in the way" -- the request is well-formed and conflicts with state the
                // provider can resolve. 400 for a malformed range, which no amount of resolving will fix.
                return messages.Any(message => message.Contains("fall inside this range"))
                    ? TypedResults.Conflict(DataResponse<CalendarBlockEntity>.Fail(messages))
                    : TypedResults.BadRequest(DataResponse<CalendarBlockEntity>.Fail(messages));
            })
            .WithName("BlockCalendar")
            .RequireAuthorization();

        calendar.MapDelete("/blocks/{email}/{identifier}",
            async Task<Results<NoContent, NotFound>> (
                IMediator mediator,
                ClaimsPrincipal user,
                string email,
                string identifier,
                CancellationToken cancellationToken) =>
            {
                OwnershipGuard.AssertOwner(user, email);

                var result = await mediator.Send(
                    new RemoveCalendarBlockCommand { EmailProvider = email, Identifier = identifier },
                    cancellationToken);

                // 404 rather than 204-regardless: unlike the device-token DELETE, whether a provider has a
                // given block of their OWN is not information withheld from them, and a silent success would
                // hide a client sending the wrong identifier.
                return result.IsSuccess ? TypedResults.NoContent() : TypedResults.NotFound();
            })
            .WithName("RemoveCalendarBlock")
            .RequireAuthorization();
    }
}
