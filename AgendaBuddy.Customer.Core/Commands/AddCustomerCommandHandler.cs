namespace AgendaBuddy.Customer.Core.Commands;

// The duplicate-email check lives here so AgendaBuddy.Customer.Api stays endpoint/DI wiring only,
// per the architecture doc.
public class AddCustomerCommandHandler(
    IMediator mediator,
    ICustomerService customerService,
    IEventStore eventStore)
    : IRequestHandler<AddCustomerCommand, Result<CustomerEntity>>
{
    public async Task<Result<CustomerEntity>> Handle(AddCustomerCommand request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        var customerEntity = request.CustomerEntity;

        // Matches by NAME, not by email, and runs before anything is persisted or published.
        var existingCustomer = await customerService.FindCustomerAsync(
            SupportTools<CustomerEntity>.FilterByNameAndLastName(customerEntity.FirstName!, customerEntity.LastName!));
        if (existingCustomer is not null)
            return Result.Fail<CustomerEntity>($"Existing record found for Email:{customerEntity.Email}");

        // Assigned once, at creation, and only when the caller supplied nothing — a later update must not
        // reshuffle somebody's avatar, and a client that does send one has chosen it deliberately.
        if (!AvatarCatalog.IsKnown(customerEntity.AvatarId))
            customerEntity.AvatarId = AvatarCatalog.Random();

        await mediator.Publish(new AddCustomerEvent { CustomerEntity = customerEntity }, cancellationToken);

        await customerService.AddCustomerAsync(customerEntity);
        await eventStore.SaveAsync(new Event
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = "Success",
            Type = nameof(AddCustomerCommand),
            Data = JsonSerializer.Serialize(customerEntity)
        });
        return Result.Ok(customerEntity);
    }
}
