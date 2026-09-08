namespace AgendaBuddy.Customer.Core.Commands;

/// <summary>
/// Records a customer's acceptance of the Terms and Conditions and the Privacy Policy.
/// </summary>
/// <remarks>
/// <b>The timestamp is the server's, not the client's.</b> A consent record whose date the consenting party
/// supplies proves nothing, and a device with a wrong clock would file the acceptance in the wrong year.
/// </remarks>
public class SetCustomerConsentCommandHandler(
    ICustomerService customerService,
    IEventStore eventStore)
    : IRequestHandler<SetCustomerConsentCommand, Result<CustomerEntity>>
{
    public async Task<Result<CustomerEntity>> Handle(
        SetCustomerConsentCommand request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        var now = DateTime.UtcNow;

        var customer = await customerService.SetConsentAsync(
            request.Email,
            request.AcceptedTerms ? now : null,
            request.AcceptedPrivacy ? now : null);

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
            Type = nameof(SetCustomerConsentCommand),
            Data = JsonSerializer.Serialize(data)
        });
}
