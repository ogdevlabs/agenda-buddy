namespace EventAndCommands.Queries.Professions;

public class GetProfessionsQuery : IRequest<IEnumerable<ProfessionEntity>>
{
    public IEnumerable<ProfessionEntity>? ProfessionEntities { get; set; }
}