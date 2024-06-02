namespace EventAndCommands.Commands.Services;

public class AddServicesToProviderCommand : IRequest<string>
{
    public string? Email { get; set; }
    public List<ServiceEntity>? ServiceEntities { get; set; }
}