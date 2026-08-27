namespace AgendaBuddy.Booking.Core.Commands;

// Constructor takes only DI-resolvable services; the per-request identifier
// comes from the command, and the handler returns Result<AppointmentEntity> instead of a
// string-sniffed convention. Typed against IBookingService/IProviderService (both interfaces already
// cover this handler's calls -- no AppendAppointmentAsync/ChangeStatusAsync needed here), so it's
// fully Moq-mockable with zero Library changes. AgendaBuddy.Booking.Api's route discards the success Value and
// answers 204 No Content unchanged (AC10) -- a JSON body cannot ride a 204 by HTTP semantics, so
// Requirement 10's blanket "every route returns DataResponse<T>" is a disclosed, deliberate exception
// here rather than silently unmet; see verification.md.
public class CancelAppointmentCommandHandler(
    IMediator mediator,
    IProviderService providerService,
    IBookingService bookingService,
    IEventStore eventStore) : IRequestHandler<CancelAppointmentCommand, Result<AppointmentEntity>>
{
    public async Task<Result<AppointmentEntity>> Handle(CancelAppointmentCommand request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        var appointmentIdentifier = request.Identifier;
        await mediator.Publish(new CancelAppointmentEvent { Identifier = appointmentIdentifier },
            cancellationToken);
        var appointmentEntity = await bookingService.SearchAppointmentAsync(appointmentIdentifier);
        if (appointmentEntity != null)
            if (await SearchAndCancelAppointment(appointmentIdentifier))
            {
                var successEvent = new Event
                {
                    Id = ObjectId.GenerateNewId(),
                    TimeStamp = DateTime.UtcNow,
                    Status = "Success",
                    Type = "CancelAppointmentCommand",
                    Data = JsonSerializer.Serialize(appointmentEntity)
                };
                await eventStore.SaveAsync(successEvent);
                return Result.Ok(appointmentEntity);
            }

        var failEvent = new Event
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = "Failed",
            Type = "CancelAppointmentCommand",
            Data = JsonSerializer.Serialize(appointmentEntity ?? new AppointmentEntity
            {
                EmailProvider = "",
                EmailCustomer = ""
            })
        };
        await eventStore.SaveAsync(failEvent);
        return Result.Fail<AppointmentEntity>(
            $"Error when trying to cancel appointment identifier: {appointmentIdentifier}");
    }

    private async Task<bool> SearchAndCancelAppointment(string identifier)
    {
        var appointment = await bookingService.SearchAppointmentAsync(identifier);
        var filter = SupportTools<ProviderEntity>.FilterByEmail(appointment.EmailProvider);
        var provider = await providerService.FindProvidersAsync(filter);
        if (provider == null) return false;
        var appointmentToRemove = provider.AppointmentEntities.SingleOrDefault(ap => ap.Identifier == identifier);
        if (appointmentToRemove == null) return false;
        var cancelAppointment = await CancelAppointment(identifier);
        if (!cancelAppointment) return false;
        provider.AppointmentEntities.Remove(appointmentToRemove);
        return await providerService.UpdateProviderAsync(provider.Id.ToString(), provider);
    }

    private async Task<bool> CancelAppointment(string identifier)
    {
        var appointment = await bookingService.SearchAppointmentAsync(identifier);

        // This used to refuse a BOOKED appointment as well as a
        // completed one, which is backwards: a booked appointment is exactly what a customer needs to be able
        // to cancel, while a completed one is history. The bug was invisible because nothing in production
        // ever set Booked — the status transitions were unenforced, so every appointment sat
        // in Requested forever and cancellation happened to work. Making transitions real activates this,
        // which is why both are fixed together: shipped separately, the status fix would have
        // looked like the cause of "customers can no longer cancel their appointments".
        if (appointment.AppointmentStatus == AppointmentStatus.Completed) return false;

        return await bookingService.CancelAppointmentAsync(identifier);
    }
}
