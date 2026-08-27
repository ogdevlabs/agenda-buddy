namespace AgendaBuddy.Customer.Domain.Queries;

[ExcludeFromCodeCoverage]
public class GetSubscribedProvidersQuery : IRequest<Result<List<string>>>
{
    public required string CustomerEmail { get; set; }
}
