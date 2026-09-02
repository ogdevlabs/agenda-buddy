namespace AgendaBuddy.Profession.Domain.Queries;

[ExcludeFromCodeCoverage]
public class GetProfessionsFromProviderQuery : IRequest<Result<List<string>>>
{
    public required string Email { get; set; }
}
