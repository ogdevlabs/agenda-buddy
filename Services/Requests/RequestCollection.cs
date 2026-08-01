namespace Services.Requests;

public class RequestCollection(IEventStore eventStore) : IRequestCollection
{
    public async Task<List<ServiceEntity>> GetServicesFromProvider(IMediator mediator,
        ProviderService providerService, string email)
    {
        var result =
            await new GetServicesFromProviderQueryHandler(mediator, providerService, email, eventStore)
                .Handle(new GetServicesFromProviderQuery(), new CancellationToken());
        return result;
    }

    public async Task<ProviderEntity> AddServicesToProvider(IMediator mediator, ProviderService providerService,
        List<ServiceEntity> serviceEntities, string email)
    {
        var result =
            await new AddServicesToProviderCommandHandler(mediator, providerService, serviceEntities,
                email, eventStore).Handle(new AddServicesToProviderCommand(), new CancellationToken());
        return result;
    }

    public async Task<ProviderEntity> UpdateServicesFromProvider(IMediator mediator, ProviderService providerService,
        List<ServiceEntity> serviceEntities,
        string email)
    {
        var result =
            await new UpdateServicesFromProviderCommandHandler(mediator, providerService, serviceEntities, email, eventStore)
                .Handle(
                    new UpdateServicesFromProviderCommand(), new CancellationToken());
        return result;
    }
}
