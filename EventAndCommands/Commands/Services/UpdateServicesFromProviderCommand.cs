namespace EventAndCommands.Commands.Services;

public class UpdateServicesFromProviderCommand : IRequest<ProviderEntity>
{
    public string? Email { get; set; }
    public List<ServiceEntity>? ServiceEntities { get; set; }
}