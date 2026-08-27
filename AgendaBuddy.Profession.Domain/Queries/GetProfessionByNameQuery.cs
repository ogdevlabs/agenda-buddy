namespace AgendaBuddy.Profession.Domain.Queries;

[ExcludeFromCodeCoverage]
public class GetProfessionByNameQuery : IRequest<Result<ProfessionEntity>>
{
    public required string Name { get; set; }
}
