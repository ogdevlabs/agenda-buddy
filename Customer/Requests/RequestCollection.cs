using EventAndCommands.Commands.Customer;
using Kafka;

namespace Customer.Requests;

public class RequestCollection(IKafkaClient kafkaClient) : IRequestCollection
{
    public async Task<string> AddCustomerRequest(IMediator mediator, CustomerService customerService,
        CustomerEntity customerEntity)
    {
        var result = await new AddCustomerCommandHandler(
                mediator, 
                (kafkaClient as KafkaClient), 
                customerService, 
                customerEntity)
            .Handle(
                new AddCustomerCommand { CustomerEntity = customerEntity},
                new CancellationToken());

        return result;
    }

    public async Task<string> UpdateCustomerRequest(IMediator mediator, CustomerService customerService,
        CustomerEntity customerEntity)
    {
        throw new NotImplementedException();
    }

    public async Task<IEnumerable<CustomerEntity>> GetCustomersRequest(IMediator mediator,
        CustomerService customerService, CustomerEntity customerEntity)
    {
        throw new NotImplementedException();
    }

    public async Task<CustomerEntity> GetCustomerByEmail(IMediator mediator, CustomerService customerService,
        string email)
    {
        throw new NotImplementedException();
    }
}