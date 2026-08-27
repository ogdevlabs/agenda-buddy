namespace AgendaBuddy.Customer.Core.Commands;

// Constructor takes only DI-resolvable services -- the per-request email comes from the command,
// not a per-instance constructor parameter.
public class UpdateCustomerCommandHandler(
    IMediator mediator,
    ICustomerService customerService,
    IEventStore eventStore)
    : IRequestHandler<UpdateCustomerCommand, Result<CustomerEntity>>
{
    public async Task<Result<CustomerEntity>> Handle(UpdateCustomerCommand request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        await mediator.Publish(new UpdateCustomerEvent { CustomerEntity = request.CustomerEntity }, cancellationToken);

        var customer = await customerService.FindCustomerAsync(SupportTools<CustomerEntity>.FilterByEmail(request.Email));
        if (customer is null)
        {
            // Type string below is a pre-existing copy-paste defect ("UpdateProviderCommand" instead of
            // "UpdateCustomerCommand") that CustomerAuditTest's own remarks record as deliberately out of
            // scope to fix. Not corrected here either.
            await eventStore.SaveAsync(new Event
            {
                Id = ObjectId.GenerateNewId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Failed",
                Type = "UpdateProviderCommand",
                Data = JsonSerializer.Serialize(request.CustomerEntity)
            });
            return Result.Fail<CustomerEntity>($"No customer found with email {request.Email}");
        }

        request.CustomerEntity.Id = customer.Id;
        request.CustomerEntity.KafkaTopic = customer.KafkaTopic;
        request.CustomerEntity.SubscribedProviderCollection = customer.SubscribedProviderCollection;
        request.CustomerEntity.AppointmentCollection = customer.AppointmentCollection;

        var updateResult = await customerService.UpdateCustomerAsync(customer.Id.ToString(), request.CustomerEntity);
        if (!updateResult)
        {
            // Pre-existing gap, preserved: no audit write on this branch, unlike the "record not found"
            // branch above -- same shape of gap Provider's own migration documented for its sibling
            // handler.
            return Result.Fail<CustomerEntity>($"Failed to update customer with email {request.Email}");
        }

        await eventStore.SaveAsync(new Event
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = "Success",
            Type = nameof(UpdateCustomerCommand),
            Data = JsonSerializer.Serialize(request.CustomerEntity)
        });
        return Result.Ok(request.CustomerEntity);
    }
}
