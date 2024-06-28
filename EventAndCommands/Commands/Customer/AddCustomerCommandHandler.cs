using EventAndCommands.Events.Customer;

namespace EventAndCommands.Commands.Customer;

[RegisterService(ServiceLifetime.Scoped)]
public class AddCustomerCommandHandler(
    IMediator mediator,
    KafkaClient kafkaClient,
    CustomerService customerService,
    CustomerEntity customerEntity) : IRequestHandler<AddCustomerCommand, string>
{
    public async Task<string> Handle(AddCustomerCommand request, CancellationToken cancellationToken)
    {
        await mediator.Publish(new AddCustomerEvent { CustomerEntity = customerEntity }, cancellationToken);
        throw new NotImplementedException();
    }
}