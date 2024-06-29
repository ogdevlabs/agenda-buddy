namespace EventAndCommands.Queries.Customers;

[ExcludeFromCodeCoverage]
public class GetCustomersQuery : IRequest<IEnumerable<CustomerEntity>>
{
    public IEnumerable<CustomerEntity>? CustomerEntities { get; }
}