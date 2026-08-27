namespace AgendaBuddy.EventAndCommands.Commands.Customer;

public class UpdateCustomerCommandHandler(
    string email,
    IMediator mediator,
    CustomerService customerService,
    CustomerEntity customerEntity,
    IEventStore eventStore) : IRequestHandler<UpdateCustomerCommand, string>
{

    public async Task<string> Handle(UpdateCustomerCommand request, CancellationToken cancellationToken)
    {
        await mediator.Publish(new UpdateCustomerEvent { CustomerEntity = request.CustomerEntity }, cancellationToken);
        var customer = await customerService.FindCustomerAsync(SupportTools<CustomerEntity>.FilterByEmail(email));
        if (customer != null)
        {
            customerEntity.Id = customer.Id;
            customerEntity.KafkaTopic = customer.KafkaTopic;
            customerEntity.SubscribedProviderCollection = customer.SubscribedProviderCollection;
            customerEntity.AppointmentCollection = customer.AppointmentCollection;

            var updateResult = await customerService.UpdateCustomerAsync(customer.Id.ToString(), customerEntity);
            if (updateResult)
            {
                var successEvent = new Event
                {
                    Id = ObjectId.GenerateNewId(),
                    TimeStamp = DateTime.UtcNow,
                    Status = "Success",
                    Type = "UpdateCustomerCommand",
                    Data = JsonSerializer.Serialize(customerEntity)
                };
                await eventStore.SaveAsync(successEvent);
                return customerEntity.ToJson();
            }
            else
            {
                var failEvent = new Event
                {
                    Id = ObjectId.GenerateNewId(),
                    TimeStamp = DateTime.UtcNow,
                    Status = "Failed",
                    Type = "UpdateProviderCommand",
                    Data = JsonSerializer.Serialize(customerEntity)
                };
                await eventStore.SaveAsync(failEvent);
            }
        }
        else
        {
            var failEvent = new Event
            {
                Id = ObjectId.GenerateNewId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Failed",
                Type = "UpdateProviderCommand",
                Data = JsonSerializer.Serialize(customerEntity)
            };
            await eventStore.SaveAsync(failEvent);
        }

        return string.Empty;
    }
}
