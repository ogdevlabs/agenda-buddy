namespace AgendaBuddy.Customer.Domain.Commands;

/// <summary>
/// Erases a customer account: removes the profile and scrubs every trace of who it belonged to.
/// </summary>
/// <remarks>
/// The <b>domain half</b> only. The Identity credential lives in a different database and is deleted by
/// <c>DELETE /api/v1/auth/account</c>, which the client calls second — the credential is what authorises this
/// command, so deleting it first would strand a live profile nothing could reach.
/// </remarks>
[ExcludeFromCodeCoverage]
public class DeleteCustomerAccountCommand : IRequest<Result<AccountErasureSummary>>
{
    public required string Email { get; set; }
}
