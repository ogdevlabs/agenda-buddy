namespace AgendaBuddy.Provider.Domain.Commands;

/// <summary>
/// Erases a provider account: removes the profile — and with it their services and embedded appointment list —
/// and scrubs every trace of who it belonged to from their customers' records.
/// </summary>
/// <remarks>
/// <para>
/// The <b>domain half</b> only. The Identity credential lives in a different database and is deleted by
/// <c>DELETE /api/v1/auth/account</c>, which the client calls second — the credential is what authorises this
/// command, so deleting it first would strand a live profile nothing could reach.
/// </para>
/// <para>
/// Distinct from <c>DeactivateProviderCommand</c>, which sets <c>IsActive=false</c> and keeps everything. That
/// one hides a provider from the directory; this one removes them.
/// </para>
/// </remarks>
[ExcludeFromCodeCoverage]
public class DeleteProviderAccountCommand : IRequest<Result<AccountErasureSummary>>
{
    public required string Email { get; set; }
}
