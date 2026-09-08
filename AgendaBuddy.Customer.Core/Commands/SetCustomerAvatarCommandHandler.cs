namespace AgendaBuddy.Customer.Core.Commands;

/// <summary>
/// Sets a customer's avatar to one of the built-in marks.
/// </summary>
/// <remarks>
/// <b>The id is checked against the catalogue this build ships, and an unknown one is refused.</b> Storing it
/// unvalidated would not throw anywhere — <c>AvatarCatalog.Resolve</c> treats an unknown id as absent and quietly
/// falls back to the email-derived mark — so the account would keep showing its old avatar with a successful save
/// behind it, which is indistinguishable from the feature not working.
/// </remarks>
public class SetCustomerAvatarCommandHandler(
    ICustomerService customerService,
    IEventStore eventStore)
    : IRequestHandler<SetCustomerAvatarCommand, Result<CustomerEntity>>
{
    public async Task<Result<CustomerEntity>> Handle(
        SetCustomerAvatarCommand request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        if (!AvatarCatalog.IsKnown(request.AvatarId))
        {
            await AuditAsync("Failed", request);
            return Result.Fail<CustomerEntity>($"'{request.AvatarId}' is not an avatar this build offers.");
        }

        var customer = await customerService.SetAvatarAsync(request.Email, request.AvatarId);
        if (customer is null)
        {
            await AuditAsync("Failed", request);
            return Result.Fail<CustomerEntity>($"No customer found with email {request.Email}");
        }

        await AuditAsync("Success", customer);
        return Result.Ok(customer);
    }

    private Task AuditAsync(string status, object data) =>
        eventStore.SaveAsync(new Event
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = status,
            Type = nameof(SetCustomerAvatarCommand),
            Data = JsonSerializer.Serialize(data)
        });
}
