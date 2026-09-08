namespace AgendaBuddy.Provider.Domain.Commands;

[ExcludeFromCodeCoverage]
public class SetProviderAvatarCommand : IRequest<Result<ProviderEntity>>
{
    public required string Email { get; set; }

    /// <summary>
    /// An id from the built-in catalogue. Validated in the handler, not merely written — see
    /// <c>SetProviderAvatarCommandHandler</c> for why an unknown id has to be refused rather than stored.
    /// </summary>
    public required string AvatarId { get; set; }
}
