#pragma warning disable CS9113 // Primary constructor parameter unused — kafkaClient reserved for future Kafka publishing
namespace Booking.Core.Commands;

// F-019-T04. See BookingAppointmentCommandHandler's remarks: constructor takes only DI-resolvable
// services; the per-request AppointmentEntity comes from the command, and the handler returns
// Result<AppointmentEntity> instead of a string-sniffed convention.
public class UpdateAppointmentCommandHandler(
    IMediator mediator,
    IKafkaClient? kafkaClient,
    ProviderService providerService,
    BookingService bookingService,
    IEventStore eventStore) : IRequestHandler<UpdateAppointmentCommand, Result<AppointmentEntity>>
{
    public async Task<Result<AppointmentEntity>> Handle(UpdateAppointmentCommand request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        var appointmentEntity = request.AppointmentEntity;
        await mediator.Publish(new UpdateAppointmentEvent { AppointmentEntity = appointmentEntity },
            cancellationToken);
        if (await SearchAndUpdateAppointment(appointmentEntity))
        {
            var successEvent = new Event
            {
                Id = ObjectId.GenerateNewId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Success",
                Type = "UpdateAppointmentCommand",
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
            Type = "UpdateAppointmentCommand",
            Data = JsonSerializer.Serialize(appointmentEntity)
        };
        await eventStore.SaveAsync(failEvent);
        return Result.Fail<AppointmentEntity>(
            $"Error when trying to update appointment identifier: {appointmentEntity.Identifier}");
    }

    private async Task<bool> SearchAndUpdateAppointment(AppointmentEntity appointmentEntity)
    {
        var identifier = appointmentEntity.Identifier;
        var filter = SupportTools<ProviderEntity>.FilterByEmail(appointmentEntity.EmailProvider);
        var provider = await providerService.FindProvidersAsync(filter);
        if (provider == null) return false;
        var appointment = provider.AppointmentEntities.FirstOrDefault(ap => ap.Identifier == identifier);
        if (appointment == null) return false;

        // F-014 requirement 13 / threat T-203: appointment status is SERVER-OWNED, so the client's value is
        // ignored here and the stored status is preserved. This line used to be
        //     appointment.AppointmentStatus = appointmentEntity.AppointmentStatus;
        // which copied whatever the caller put in the request body, bypassing AppointmentEntity.Book() and
        // .Complete() entirely — the two methods that hold the transition rules and were, as a result, dead
        // code. A customer could mark a brand-new appointment Completed, asserting that work was delivered.
        // Status changes now go through POST /api/v1/booking/appointments/{identifier}/status, which applies
        // the transition through the entity. The description is derived from the status for the same reason:
        // it is a rendering of the status, so accepting it from the caller would let the two disagree.
        appointment.Start = appointmentEntity.Start;
        appointment.End = appointmentEntity.End;
        var updateAppointment = await UpdateAppointment(identifier, appointment);
        if (!updateAppointment) return false;
        return await providerService.UpdateProviderAsync(provider.Id.ToString(), provider);
    }

    private async Task<bool> UpdateAppointment(string identifier, AppointmentEntity appointment)
    {
        return await bookingService.UpdateAppointmentAsync(identifier, appointment);
    }
}
