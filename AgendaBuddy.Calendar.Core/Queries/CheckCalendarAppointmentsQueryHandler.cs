namespace AgendaBuddy.Calendar.Core.Queries;

using AgendaBuddy.Library.Dtos;

// See CheckCalendarAvailabilityQueryHandler's remarks -- same shape, same interface
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

        // A Customer's own calendar has to be gathered from the provider side, because appointments are
        // embedded in the PROVIDER's document -- CustomerEntity.AppointmentCollection is only identifier
        // strings. Looking the address up as a provider and failing when it matched none meant a Customer
        // got 404 for their own appointments, always: the same booking answered 200 for the provider and
        // 404 for the customer on it. The route's caller is already ownership-guarded to this address.
        if (providerEntity is null)
        {
            var customerAppointments = await providerService.FindAppointmentsByCustomerAsync(request.Email);
            await eventStore.SaveAsync(QueryAudit.Success(nameof(CheckCalendarAppointmentsQuery), customerAppointments.Count));
            return Result.Ok(customerAppointments);
        }

        var appointments = providerEntity.AppointmentEntities;
        await eventStore.SaveAsync(QueryAudit.Success(nameof(CheckCalendarAppointmentsQuery), appointments.Count));
        return Result.Ok(appointments);
    }
}

public class GetAppointmentsPageQueryHandler(
    IProviderService providerService,
    IEventStore eventStore) : IRequestHandler<GetAppointmentsPageQuery, Result<PagedResponse<AppointmentEntity>>>
{
    public async Task<Result<PagedResponse<AppointmentEntity>>> Handle(
        GetAppointmentsPageQuery request,
        CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        var filterProvider = SupportTools<ProviderEntity>.FilterByEmail(request.Email);
        var provider = await providerService.FindProvidersAsync(filterProvider);
        var appointments = provider is null
            ? await providerService.FindAppointmentsByCustomerAsync(request.Email)
            : provider.AppointmentEntities;

        var matching = appointments
            .Where(appointment => request.Segment switch
            {
                AppointmentListSegment.Scheduled =>
                    appointment.AppointmentStatus is not (AppointmentStatus.Completed or AppointmentStatus.Cancelled)
                    && EffectiveStart(appointment) >= request.NowUtc,
                AppointmentListSegment.Done =>
                    appointment.AppointmentStatus != AppointmentStatus.Cancelled
                    && (appointment.AppointmentStatus == AppointmentStatus.Completed
                        || EffectiveStart(appointment) < request.NowUtc),
                AppointmentListSegment.Cancelled =>
                    appointment.AppointmentStatus == AppointmentStatus.Cancelled,
                _ => false
            })
            .ToList();

        matching = request.Segment == AppointmentListSegment.Scheduled
            ? matching.OrderBy(EffectiveStart).ToList()
            : matching.OrderByDescending(EffectiveStart).ToList();

        var page = matching
            .Skip(request.Page.Skip)
            .Take(request.Page.PageSize)
            .ToList();

        await eventStore.SaveAsync(QueryAudit.Success(nameof(GetAppointmentsPageQuery), page.Count));

        return Result.Ok(PagedResponse<AppointmentEntity>.From(page, matching.Count, request.Page));
    }

    private static DateTime EffectiveStart(AppointmentEntity appointment) =>
        appointment.AppointmentStatus == AppointmentStatus.RescheduleRequested
        && appointment.ProposedStart.HasValue
            ? appointment.ProposedStart.Value
            : appointment.Start;
}
