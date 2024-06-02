namespace Services.Requests;

public interface IRequestCollection
{
    public Task<IEnumerable<ServiceEntity>> GetServicesFromProvider(IMediator mediator, ProviderService providerService,
        string email);

    public Task<ProviderEntity> AddServicesToProvider(IMediator mediator, ProviderService providerService,
        List<ServiceEntity> serviceEntities, string email);
    
}