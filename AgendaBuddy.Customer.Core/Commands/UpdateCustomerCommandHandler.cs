namespace AgendaBuddy.Customer.Core.Commands;

// F-020-T12: moved from AgendaBuddy.EventAndCommands.Commands.Customer. Constructor takes only
// DI-resolvable services -- the pre-refactor handler took `email` as a per-instance constructor
// parameter (Requests/RequestCollection.cs, deleted); it now comes from the command.
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
            // Preserved verbatim from AgendaBuddy.EventAndCommands.Commands.Customer.UpdateCustomerCommandHandler
            // (deleted), including its Type string: a pre-existing copy-paste defect ("UpdateProviderCommand"
            // instead of "UpdateCustomerCommand") that CustomerAuditTest's own remarks record as deliberately
            // out of scope to fix (F-018-T13). Not corrected here either -- this task's recipe is
            // envelope/dispatch only, not incidental bug fixes.
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
