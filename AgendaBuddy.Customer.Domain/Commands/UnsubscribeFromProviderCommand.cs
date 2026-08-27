namespace AgendaBuddy.Customer.Domain.Commands;

[ExcludeFromCodeCoverage]
public class UnsubscribeFromProviderCommand : IRequest<Result<CustomerEntity>>
{
    public required string CustomerEmail { get; set; }
    public required string ProviderEmail { get; set; }
}
