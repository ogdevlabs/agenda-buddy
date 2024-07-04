namespace Services.Requests;

public interface IRequestCollection
{
    public Task<List<ServiceEntity>> GetServicesFromProvider(IMediator mediator, ProviderService providerService,
        string email);

    public Task<ProviderEntity> AddServicesToProvider(IMediator mediator, ProviderService providerService,
        List<ServiceEntity> serviceEntities, string email);

    public Task<ProviderEntity> UpdateServicesFromProvider(IMediator mediator, ProviderService providerService,
        List<ServiceEntity> serviceEntities, string email);
}