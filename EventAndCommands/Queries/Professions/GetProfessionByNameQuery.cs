namespace EventAndCommands.Queries.Professions;

[ExcludeFromCodeCoverage]
public class GetProfessionByNameQuery : IRequest<ProfessionEntity>
{
    public required string Name { get; set; }
}