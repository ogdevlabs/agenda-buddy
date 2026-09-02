namespace AgendaBuddy.Profession.Domain.Commands;

[ExcludeFromCodeCoverage]
public class RemoveProfessionFromProviderCommand : IRequest<Result<List<string>>>
{
    public required string Email { get; set; }
    public required string ProfessionName { get; set; }
}
