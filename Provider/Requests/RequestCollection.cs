namespace Provider.Requests;

public class RequestCollection : IRequestCollection
{
    public async Task<string> AddProviderRequest(
        IMediator mediator,
        ProviderService providerService,
        ProviderEntity providerEntity)
    {
        var result = await new AddProviderCommandHandler(
                mediator,
                providerService,
                providerEntity)
            .Handle(new AddProviderCommand(), new CancellationToken());
        return result;
    }

    public async Task<string> UpdateProviderRequest(
        string email,
        IMediator mediator,
        ProviderService providerService,
        ProviderEntity providerEntity)
    {
        var result = await new UpdateProviderCommandHandler(
                email,
                mediator,
                providerService,
                providerEntity)
            .Handle(
                new UpdateProviderCommand { ProviderEntity = providerEntity },
                new CancellationToken());
        return result;
    }

    public async Task<List<ProviderEntity>?> GetProvidersRequest(IMediator mediator,
        ProviderService providerService)
    {
        var result =
            await new GetProvidersQueryHandler(mediator, providerService).Handle(new GetProvidersQuery(),
                new CancellationToken());
        return result;
    }

    public async Task<ProviderEntity> GetProviderByEmail(IMediator mediator, ProviderService providerService,
        string email)
    {
        var result =
            await new GetProviderByEmailQueryHandler(mediator, providerService, email).Handle(
                new GetProviderByEmailQuery(), new CancellationToken());
        return result;
    }
}