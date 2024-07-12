namespace EventAndCommands.Commands.Profession;

public class AddProfessionsToProviderCommand : IRequest<ProviderEntity>
{
    public List<ProfessionEntity>? ProfessionEntities { get; set; }
}