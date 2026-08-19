namespace Provider.Requests;

public interface IRequestCollection
{
    public Task<string> AddProviderRequest(
        IMediator mediator,
        ProviderService providerService,
        ProviderEntity providerEntity);

    public Task<string> UpdateProviderRequest(
        string email,
        IMediator mediator,
        ProviderService providerService,
        ProviderEntity providerEntity);

    public Task<PagedResponse<ProviderEntity>> GetProvidersRequest(IMediator mediator,
        ProviderService providerService, PageRequest page);

    public Task<ProviderEntity> GetProviderByEmail(IMediator mediator, ProviderService providerService, string email);
}