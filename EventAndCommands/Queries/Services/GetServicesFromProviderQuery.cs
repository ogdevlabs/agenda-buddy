namespace EventAndCommands.Queries.Services;

public class GetServicesFromProviderQuery : IRequest<IEnumerable<ServiceEntity>>
{
    public IEnumerable<ServiceEntity>? ServiceEntities { get; set; }
}