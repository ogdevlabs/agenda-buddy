namespace EventAndCommands.Commands.Services;

[ExcludeFromCodeCoverage]
public class AddServicesToProviderCommand : IRequest<ProviderEntity>
{
    public string? Email { get; set; }
    public List<ServiceEntity>? ServiceEntities { get; set; }
}
