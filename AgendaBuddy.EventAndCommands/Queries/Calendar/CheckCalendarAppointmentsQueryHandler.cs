namespace AgendaBuddy.EventAndCommands.Queries.Calendar;

public class
    CheckCalendarAppointmentsQueryHandler(
        IMediator mediator,
        ProviderService providerService,
        string email,
        IEventStore eventStore) : IRequestHandler<CheckCalendarAppointmentsQuery, List<AppointmentEntity>>
{

    public async Task<List<AppointmentEntity>> Handle(CheckCalendarAppointmentsQuery request,
        CancellationToken cancellationToken)
    {
        await mediator.Publish(new CheckCalendarAppointmentsEvent { Email = email }, cancellationToken);
        var filterProvider = SupportTools<ProviderEntity>.FilterByEmail(email);
        var providerEntity = await providerService.FindProvidersAsync(filterProvider);
        if (providerEntity != null)
        {
            var providerAppointmentCollection = providerEntity.AppointmentEntities;

            // Counts the appointments actually disclosed, not "one provider matched" — the count is
            // there to answer "how much was read".
            await eventStore.SaveAsync(QueryAudit.Success(
                "CheckCalendarAppointmentsQuery", providerAppointmentCollection.Count));
            return providerAppointmentCollection;
        }

        await eventStore.SaveAsync(QueryAudit.Failure("CheckCalendarAppointmentsQuery"));
        return null!;
    }
}
