namespace EventAndCommands.Queries.Professions;

[ExcludeFromCodeCoverage]
public class GetProfessionsQuery : IRequest<IEnumerable<ProfessionEntity>>
{
    public IEnumerable<ProfessionEntity>? ProfessionEntities { get; set; }
}