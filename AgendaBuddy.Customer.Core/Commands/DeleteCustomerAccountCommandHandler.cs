namespace AgendaBuddy.Customer.Core.Commands;

/// <summary>
/// Erases a customer account's domain side.
/// </summary>
/// <remarks>
/// <para>
/// <b>A missing profile still succeeds.</b> The one class of account that most needs deleting is the one whose
/// profile creation failed — registration writes an Identity credential and then a domain profile, and if the
/// second call fails the account exists, can sign in, and has no profile. Refusing to erase it because there is
/// nothing to find would leave it permanently undeletable, which is exactly the state App Review Guideline
/// 5.1.1(v) exists to prevent.
/// </para>
/// <para>
/// The audit event carries the erasure summary — counts and the tombstone — and deliberately <b>not</b> the
/// address being erased on the success path. An audit trail that records what was deleted alongside who it
/// belonged to is a copy of the data the deletion was supposed to remove.
/// </para>
/// </remarks>
public class DeleteCustomerAccountCommandHandler(
    IAccountErasureService erasureService,
    IEventStore eventStore)
    : IRequestHandler<DeleteCustomerAccountCommand, Result<AccountErasureSummary>>
{
    public async Task<Result<AccountErasureSummary>> Handle(
        DeleteCustomerAccountCommand request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        AccountErasureSummary summary;

        try
        {
            summary = await erasureService.EraseCustomerAsync(request.Email);
        }
        catch (Exception exception)
        {
            // The address is in the failure record on purpose, and only here: a half-finished erasure has to be
            // completable, and it cannot be retried against an account nobody can name.
            await AuditAsync("Failed", new { request.Email, Error = exception.Message });
            return Result.Fail<AccountErasureSummary>("Could not erase the account.");
        }

        await AuditAsync("Success", summary);
        return Result.Ok(summary);
    }

    private Task AuditAsync(string status, object data) =>
        eventStore.SaveAsync(new Event
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = status,
            Type = nameof(DeleteCustomerAccountCommand),
            Data = JsonSerializer.Serialize(data)
        });
}
