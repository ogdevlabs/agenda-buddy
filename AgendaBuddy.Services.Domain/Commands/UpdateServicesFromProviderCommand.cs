namespace AgendaBuddy.Services.Domain.Commands;

[ExcludeFromCodeCoverage]
public class UpdateServicesFromProviderCommand : IRequest<Result<ProviderEntity>>
{
    public required string Email { get; set; }
    public required List<ServiceEntity> ServiceEntities { get; set; }
}
