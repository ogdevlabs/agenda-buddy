namespace AgendaBuddy.Provider.Core.Commands;

/// <summary>
/// Erases a provider account's domain side.
/// </summary>
/// <remarks>
/// <para>
/// <b>A missing profile still succeeds</b>, for the reason spelled out on
/// <c>DeleteCustomerAccountCommandHandler</c>: an account whose profile creation failed is the one that most
/// needs to be deletable, and refusing because there is nothing to find would make it permanently undeletable.
/// </para>
/// <para>
/// The audit event carries the erasure summary and, on success, deliberately not the address — an audit trail
/// recording what was deleted next to whose it was is a copy of the data the deletion removed.
/// </para>
/// </remarks>
public class DeleteProviderAccountCommandHandler(
    IAccountErasureService erasureService,
    IEventStore eventStore)
    : IRequestHandler<DeleteProviderAccountCommand, Result<AccountErasureSummary>>
{
    public async Task<Result<AccountErasureSummary>> Handle(
        DeleteProviderAccountCommand request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        AccountErasureSummary summary;

        try
        {
            summary = await erasureService.EraseProviderAsync(request.Email);
        }
        catch (Exception exception)
        {
            // The address is in the failure record on purpose, and only here: a half-finished erasure has to be
            // completable, and it cannot be retried against an account nobody can name.
            await SaveAsync("Failed", new { request.Email, Error = exception.Message });
            return Result.Fail<AccountErasureSummary>("Could not erase the account.");
        }

        await SaveAsync("Success", summary);
        return Result.Ok(summary);
    }

    private Task SaveAsync(string status, object data) =>
        eventStore.SaveAsync(new Event
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = status,
            Type = nameof(DeleteProviderAccountCommand),
            Data = JsonSerializer.Serialize(data)
        });
}
