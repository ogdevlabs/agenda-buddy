namespace AgendaBuddy.EventAndCommands.Queries.Customers;

[ExcludeFromCodeCoverage]
public class GetCustomerByEmailQuery : IRequest<CustomerEntity>
{
    public CustomerEntity? CustomerEntity { get; set; }
}
