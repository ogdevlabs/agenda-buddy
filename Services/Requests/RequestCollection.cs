namespace Services.Requests;

public class RequestCollection : IRequestCollection
{
    public async Task<IEnumerable<ServiceEntity>> GetServicesFromProvider(IMediator mediator,
        ProviderService providerService, string email)
    {
        var result =
            await new GetServicesFromProviderQueryHandler(mediator, providerService, email)
                .Handle(new GetServicesFromProviderQuery(), new CancellationToken());
        return result;
    }

    public async Task<ProviderEntity> AddServicesToProvider(IMediator mediator, ProviderService providerService,
        List<ServiceEntity> serviceEntities, string email)
    {
        var result =
            await new AddServicesToProviderCommandHandler(mediator, providerService, serviceEntities,
                email).Handle(new AddServicesToProviderCommand(), new CancellationToken());
        return result;
    }
}