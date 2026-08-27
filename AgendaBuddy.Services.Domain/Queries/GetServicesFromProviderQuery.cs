namespace AgendaBuddy.Services.Domain.Queries;

[ExcludeFromCodeCoverage]
public class GetServicesFromProviderQuery : IRequest<Result<List<ServiceEntity>>>
{
    public required string Email { get; set; }
}
