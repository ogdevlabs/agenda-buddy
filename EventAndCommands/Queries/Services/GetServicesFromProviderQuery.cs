namespace EventAndCommands.Queries.Services;

[ExcludeFromCodeCoverage]
public class GetServicesFromProviderQuery : IRequest<List<ServiceEntity>>
{
    public List<ServiceEntity>? ServiceEntities { get; set; }
}