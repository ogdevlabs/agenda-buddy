namespace EventAndCommands.Queries.Services;

[ExcludeFromCodeCoverage]
public class GetServicesFromProviderQuery : IRequest<IEnumerable<ServiceEntity>>
{
    public IEnumerable<ServiceEntity>? ServiceEntities { get; set; }
}