namespace AgendaBuddy.Profession.Domain.Commands;

[ExcludeFromCodeCoverage]
public class AddProfessionsToProviderCommand : IRequest<Result<List<string>>>
{
    public required string Email { get; set; }
    public required List<string> ProfessionNames { get; set; }
}
