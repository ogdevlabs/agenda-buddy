namespace AgendaBuddy.EventAndCommands.Queries.Professions;

[ExcludeFromCodeCoverage]
public class GetProfessionsQuery : IRequest<List<ProfessionEntity>>
{
    public List<ProfessionEntity>? ProfessionEntities { get; set; }
}
