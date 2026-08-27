namespace AgendaBuddy.Calendar.Core.Queries;

// F-020-T08. See CheckCalendarAvailabilityQueryHandler's remarks -- same shape, same interface
// choice, same absence of any ICalendarService call site.
public class CheckCalendarAppointmentsQueryHandler(
    IMediator mediator,
    IProviderService providerService,
    IEventStore eventStore) : IRequestHandler<CheckCalendarAppointmentsQuery, Result<List<AppointmentEntity>>>
{
    public async Task<Result<List<AppointmentEntity>>> Handle(CheckCalendarAppointmentsQuery request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        await mediator.Publish(new CheckCalendarAppointmentsEvent { Email = request.Email }, cancellationToken);

        var filterProvider = SupportTools<ProviderEntity>.FilterByEmail(request.Email);
        var providerEntity = await providerService.FindProvidersAsync(filterProvider);
        if (providerEntity is null)
        {
            await eventStore.SaveAsync(QueryAudit.Failure(nameof(CheckCalendarAppointmentsQuery)));
            return Result.Fail<List<AppointmentEntity>>("No provider found for this email.");
        }

        var appointments = providerEntity.AppointmentEntities;
        await eventStore.SaveAsync(QueryAudit.Success(nameof(CheckCalendarAppointmentsQuery), appointments.Count));
        return Result.Ok(appointments);
    }
}
