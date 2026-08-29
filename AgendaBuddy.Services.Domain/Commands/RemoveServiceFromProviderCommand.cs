namespace AgendaBuddy.Services.Domain.Commands;

[ExcludeFromCodeCoverage]
public class RemoveServiceFromProviderCommand : IRequest<Result<ProviderEntity>>
{
    public required string Email { get; set; }
    public required string ServiceName { get; set; }
}
