namespace AgendaBuddy.Booking.Api.Modules;

public class BookingModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var booking = app.MapGroup("api/v1/booking")
            .WithTags("BookingAPI")
            .WithOpenApi()
            .AddEndpointFilter<ProblemDetailsServiceEndpointFilter>();

        booking.MapPost("/appointments",
                async Task<Results<ValidationProblem, ForbidHttpResult, Created<DataResponse<AppointmentEntity>>, BadRequest<DataResponse<AppointmentEntity>>>> (
                    IMediator mediator,
                    ClaimsPrincipal user,
                    AppointmentEntity appointmentEntity,
                    IValidator<AppointmentEntity> appointmentValidator,
                    CancellationToken cancellationToken) =>
                {
                    // This is the one route swapped from MiniValidator.TryValidate to
                    // Validot for a real vertical-slice comparison. The other two original routes below (PUT,
                    // DELETE) are deliberately untouched.
                    var validationResult = appointmentValidator.Validate(appointmentEntity);
                    if (validationResult.AnyErrors)
                        return TypedResults.ValidationProblem(
                            validationResult.MessageMap.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray()));

                    // Either the provider or customer booking on behalf of themselves
                    try { OwnershipGuard.AssertOwnerAny(user, appointmentEntity.EmailProvider, appointmentEntity.EmailCustomer); }
                    catch (ForbiddenException) { return TypedResults.Forbid(); }

                    var result = await mediator.Send(new BookAppointmentCommand { AppointmentEntity = appointmentEntity },
                        cancellationToken);

                    if (result.IsSuccess)
                        return TypedResults.Created($"/api/v1/appointments/{appointmentEntity.Identifier}",
                            DataResponse<AppointmentEntity>.Ok(result.Value));
                    return TypedResults.BadRequest(
                        DataResponse<AppointmentEntity>.Fail(result.Errors.Select(e => e.Message)));
                })
            .WithName("BookAppointment")
            .RequireAuthorization();

        booking.MapPut("/appointments/",
                async Task<Results<ValidationProblem, ForbidHttpResult, Accepted<DataResponse<AppointmentEntity>>, BadRequest<DataResponse<AppointmentEntity>>>> (
                    IMediator mediator,
                    ClaimsPrincipal user,
                    AppointmentEntity appointmentEntity,
                    CancellationToken cancellationToken) =>
                {
                    if (!MiniValidator.TryValidate(appointmentEntity, out var errors))
                        return TypedResults.ValidationProblem(errors);

                    try { OwnershipGuard.AssertOwnerAny(user, appointmentEntity.EmailProvider, appointmentEntity.EmailCustomer); }
                    catch (ForbiddenException) { return TypedResults.Forbid(); }

                    var result = await mediator.Send(new UpdateAppointmentCommand { AppointmentEntity = appointmentEntity },
                        cancellationToken);

                    if (result.IsSuccess)
                        return TypedResults.Accepted($"/api/v1/appointments/{appointmentEntity.Identifier}",
                            DataResponse<AppointmentEntity>.Ok(result.Value));
                    return TypedResults.BadRequest(
                        DataResponse<AppointmentEntity>.Fail(result.Errors.Select(e => e.Message)));
                })
            .WithName("UpdateAppointment")
            .RequireAuthorization();

        booking.MapDelete("/appointments/",
                async Task<Results<ValidationProblem, ForbidHttpResult, NoContent, BadRequest<DataResponse<AppointmentEntity>>>> (
                    IMediator mediator,
                    ClaimsPrincipal user,
                    [FromBody] AppointmentEntity appointmentEntity,
                    CancellationToken cancellationToken) =>
                {
                    if (!MiniValidator.TryValidate(appointmentEntity, out var errors))
                        return TypedResults.ValidationProblem(errors);

                    try { OwnershipGuard.AssertOwnerAny(user, appointmentEntity.EmailProvider, appointmentEntity.EmailCustomer); }
                    catch (ForbiddenException) { return TypedResults.Forbid(); }

                    var result = await mediator.Send(new CancelAppointmentCommand { Identifier = appointmentEntity.Identifier },
                        cancellationToken);

                    // A 204 cannot carry a body by HTTP semantics, so the success Value is discarded here rather
                    // than forced into an envelope -- a disclosed exception to Requirement 10's blanket claim, not
                    // a silent one. See CancelAppointmentCommandHandler's remarks and T11's final verification.
                    if (result.IsSuccess) return TypedResults.NoContent();
                    return TypedResults.BadRequest(
                        DataResponse<AppointmentEntity>.Fail(result.Errors.Select(e => e.Message)));
                })
            .WithName("CancelAppointment")
            .RequireAuthorization();

        // ── Appointment status, session notes, payments ───────────────────────────────────────────
        //
        const string ProviderRole = "Provider";
        //
        // Every route here is authenticated, ownership-guarded, and role-checked
        // where a role distinction exists — five routes in this solution once returned PII to anonymous
        // callers, and the fix was a guard on every route.

        // Status is SERVER-OWNED: the PUT above ignores the field, and this is
        // the only way to change it. The transition runs through AppointmentEntity.TransitionTo, so Book() and
        // Complete() — dead code until now — hold the rules.
        booking.MapPost("/appointments/{identifier}/status",
                async Task<Results<Ok<DataResponse<AppointmentStatusResponse>>, ForbidHttpResult, NotFound, Conflict<string>, BadRequest<string>>> (
                    string identifier,
                    ClaimsPrincipal user,
                    AppointmentStatusRequest request,
                    BookingService bookingService,
                    IMediator mediator,
                    CancellationToken cancellationToken) =>
                {
                    // Enum.TryParse also accepts the NUMERIC form, and — less obviously — accepts undefined numbers:
                    // TryParse<AppointmentStatus>("99") succeeds with the value 99. Enum.IsDefined is what turns that
                    // into a 400 rather than letting it reach the transition and answer 409, which would imply the state
                    // exists and merely conflicts.
                    if (request is null
                        || !Enum.TryParse<AppointmentStatus>(request.Status, ignoreCase: true, out var target)
                        || !Enum.IsDefined(target))
                    {
                        return TypedResults.BadRequest("status must be one of: Booked, Completed.");
                    }

                    var appointment = await bookingService.SearchAppointmentAsync(identifier);
                    if (appointment is null) return TypedResults.NotFound();

                    // Either participant may book; only the provider may complete. A customer marking their own
                    // session complete is a claim about work delivered, not a scheduling action.
                    try
                    {
                        OwnershipGuard.AssertOwnerAny(user, appointment.EmailProvider, appointment.EmailCustomer);
                        if (target == AppointmentStatus.Completed)
                            OwnershipGuard.AssertOwner(user, appointment.EmailProvider);
                    }
                    catch (ForbiddenException) { return TypedResults.Forbid(); }

                    try
                    {
                        var result = await mediator.Send(
                            new ChangeAppointmentStatusCommand { Identifier = identifier, TargetStatus = target },
                            cancellationToken);

                        if (result.IsFailed) return TypedResults.NotFound();
                    }
                    catch (InvalidOperationException ex)
                    {
                        // The entity's own guard refused the transition. 409 rather than 400: the request is
                        // well-formed, it conflicts with the current state.
                        return TypedResults.Conflict(ex.Message);
                    }

                    return TypedResults.Ok(DataResponse<AppointmentStatusResponse>.Ok(
                        new AppointmentStatusResponse(identifier, target.ToString())));
                })
            .WithName("ChangeAppointmentStatus")
            .RequireAuthorization();

        // ── Session notes — the most sensitive data in the product ───────────────────────────────────────────
        //
        // The owning provider is taken from the CALLER'S TOKEN and never from the request. NoteService
        // asks for a providerEmail, and a route that passed a client-supplied one through would hand any
        // authenticated caller every provider's notes for any appointment identifier they can guess — identifiers a
        // customer already receives in their own appointment responses.
        //
        // KeyNotFoundException and UnauthorizedAccessException BOTH map to 403, so a caller cannot
        // tell "someone else's note" from "no such note". For a therapist, the existence of a note is itself
        // disclosure.
        booking.MapGet("/appointments/{identifier}/notes",
                async Task<Results<Ok<DataResponse<IEnumerable<NoteEntity>>>, ForbidHttpResult>> (
                    string identifier, ClaimsPrincipal user, BookingService bookingService,
                    IMediator mediator, CancellationToken cancellationToken) =>
                {
                    var providerEmail = user.FindFirstValue(ClaimTypes.NameIdentifier);

                    try
                    {
                        OwnershipGuard.AssertRole(user, ProviderRole);

                        var appointment = await bookingService.SearchAppointmentAsync(identifier);
                        // A missing appointment answers 403 alongside a foreign one: distinguishing them would turn
                        // this route into an appointment-existence oracle for any authenticated provider.
                        OwnershipGuard.AssertOwner(user, appointment?.EmailProvider);
                    }
                    catch (ForbiddenException) { return TypedResults.Forbid(); }

                    var result = await mediator.Send(
                        new GetAppointmentNotesQuery { ProviderEmail = providerEmail!, Identifier = identifier },
                        cancellationToken);
                    return TypedResults.Ok(DataResponse<IEnumerable<NoteEntity>>.Ok(result.Value));
                })
            .WithName("GetAppointmentNotes")
            .RequireAuthorization();

        booking.MapPost("/appointments/{identifier}/notes",
                async Task<Results<Created<DataResponse<NoteEntity>>, ForbidHttpResult, BadRequest<string>>> (
                    string identifier, ClaimsPrincipal user, NoteRequest request,
                    BookingService bookingService, IMediator mediator, IValidator<NoteRequest> noteValidator,
                    CancellationToken cancellationToken) =>
                {
                    // Party Review: replaces the inline IsNullOrWhiteSpace check with NoteSpec, wired here
                    // for the first time (authored, unwired, since T02). Same failure branch shape (BadRequest<string>).
                    if (noteValidator.Validate(request).AnyErrors)
                        return TypedResults.BadRequest("content is required.");

                    var providerEmail = user.FindFirstValue(ClaimTypes.NameIdentifier);

                    try
                    {
                        OwnershipGuard.AssertRole(user, ProviderRole);
                        var appointment = await bookingService.SearchAppointmentAsync(identifier);
                        OwnershipGuard.AssertOwner(user, appointment?.EmailProvider);
                    }
                    catch (ForbiddenException) { return TypedResults.Forbid(); }

                    // providerEmail from the token, appointmentIdentifier from the path. A body carrying either is
                    // ignored — NoteRequest has no such field, which is the cheapest way to guarantee it.
                    var result = await mediator.Send(
                        new CreateAppointmentNoteCommand
                        {
                            ProviderEmail = providerEmail!,
                            Identifier = identifier,
                            Content = request.Content
                        },
                        cancellationToken);

                    return TypedResults.Created($"/api/v1/booking/notes/{result.Value.Id}", DataResponse<NoteEntity>.Ok(result.Value));
                })
            .WithName("CreateAppointmentNote")
            .RequireAuthorization();

        booking.MapPut("/notes/{id}",
                async Task<Results<Ok<DataResponse<NoteEntity>>, ForbidHttpResult, BadRequest<string>>> (
                    string id, ClaimsPrincipal user, NoteRequest request,
                    IMediator mediator, IValidator<NoteRequest> noteValidator, CancellationToken cancellationToken) =>
                {
                    if (noteValidator.Validate(request).AnyErrors)
                        return TypedResults.BadRequest("content is required.");

                    var providerEmail = user.FindFirstValue(ClaimTypes.NameIdentifier);

                    try
                    {
                        OwnershipGuard.AssertRole(user, ProviderRole);
                        if (providerEmail is null) throw new ForbiddenException();

                        var result = await mediator.Send(
                            new UpdateAppointmentNoteCommand { Id = id, ProviderEmail = providerEmail, Content = request.Content },
                            cancellationToken);
                        return TypedResults.Ok(DataResponse<NoteEntity>.Ok(result.Value));
                    }
                    // Both causes answer the same way, deliberately.
                    catch (ForbiddenException) { return TypedResults.Forbid(); }
                    catch (UnauthorizedAccessException) { return TypedResults.Forbid(); }
                    catch (KeyNotFoundException) { return TypedResults.Forbid(); }
                })
            .WithName("UpdateAppointmentNote")
            .RequireAuthorization();

        booking.MapDelete("/notes/{id}",
                async Task<Results<NoContent, ForbidHttpResult>> (
                    string id, ClaimsPrincipal user, IMediator mediator, CancellationToken cancellationToken) =>
                {
                    var providerEmail = user.FindFirstValue(ClaimTypes.NameIdentifier);

                    try
                    {
                        OwnershipGuard.AssertRole(user, ProviderRole);
                        if (providerEmail is null) throw new ForbiddenException();

                        await mediator.Send(
                            new DeleteAppointmentNoteCommand { Id = id, ProviderEmail = providerEmail }, cancellationToken);
                        // A 204 cannot carry a body by HTTP semantics -- same disclosed exception to
                        // Requirement 10's blanket claim as AgendaBuddy.Booking.Api's Cancel route.
                        return TypedResults.NoContent();
                    }
                    catch (ForbiddenException) { return TypedResults.Forbid(); }
                    catch (UnauthorizedAccessException) { return TypedResults.Forbid(); }
                    catch (KeyNotFoundException) { return TypedResults.Forbid(); }
                })
            .WithName("DeleteAppointmentNote")
            .RequireAuthorization();

        // ── Payments ────────────────────────────────────────────────────────────────────────────────────────
        //
        // Both participant emails come from the STORED APPOINTMENT, never from the body, so a caller
        // cannot record a payment against someone else. A second charge for the same appointment answers 409.
        //
        // ⚠️ RESIDUAL, ACCEPTED: `amount` is client-supplied and there is nothing to validate it against, because an
        // appointment does not record which service it was booked for. With the default non-charging gateway a wrong
        // amount corrupts a record; with a real Stripe key it would be a real underpayment.
        booking.MapPost("/appointments/{identifier}/payment",
                async Task<Results<Created<DataResponse<PaymentEntity>>, ForbidHttpResult, NotFound, Conflict<string>, BadRequest<string>>> (
                    string identifier, ClaimsPrincipal user, PaymentRequest request,
                    BookingService bookingService, IMediator mediator, CancellationToken cancellationToken) =>
                {
                    if (request is null || request.Amount <= 0)
                        return TypedResults.BadRequest("amount must be greater than zero.");

                    var appointment = await bookingService.SearchAppointmentAsync(identifier);
                    if (appointment is null) return TypedResults.NotFound();

                    try { OwnershipGuard.AssertOwnerAny(user, appointment.EmailProvider, appointment.EmailCustomer); }
                    catch (ForbiddenException) { return TypedResults.Forbid(); }

                    try
                    {
                        var result = await mediator.Send(
                            new PayForAppointmentCommand
                            {
                                Identifier = identifier,
                                ProviderEmail = appointment.EmailProvider,
                                CustomerEmail = appointment.EmailCustomer,
                                Amount = request.Amount,
                                Currency = string.IsNullOrWhiteSpace(request.Currency) ? "usd" : request.Currency
                            },
                            cancellationToken);

                        return TypedResults.Created(
                            $"/api/v1/booking/appointments/{identifier}/payment", DataResponse<PaymentEntity>.Ok(result.Value));
                    }
                    catch (InvalidOperationException ex)
                    {
                        // 409 rather than 400: the request is well-formed, it
                        // conflicts with the current state (already paid).
                        return TypedResults.Conflict(ex.Message);
                    }
                })
            .WithName("PayForAppointment")
            .RequireAuthorization();

        booking.MapGet("/appointments/{identifier}/payment",
                async Task<Results<Ok<DataResponse<PaymentEntity>>, ForbidHttpResult, NotFound>> (
                    string identifier, ClaimsPrincipal user,
                    BookingService bookingService, IMediator mediator, CancellationToken cancellationToken) =>
                {
                    var appointment = await bookingService.SearchAppointmentAsync(identifier);
                    if (appointment is null) return TypedResults.NotFound();

                    try { OwnershipGuard.AssertOwnerAny(user, appointment.EmailProvider, appointment.EmailCustomer); }
                    catch (ForbiddenException) { return TypedResults.Forbid(); }

                    // 404 is safe here: the caller has already proven they are a participant in this appointment.
                    var result = await mediator.Send(new GetAppointmentPaymentQuery { Identifier = identifier }, cancellationToken);
                    return result.IsFailed ? TypedResults.NotFound() : TypedResults.Ok(DataResponse<PaymentEntity>.Ok(result.Value));
                })
            .WithName("GetAppointmentPayment")
            .RequireAuthorization();
    }
}
