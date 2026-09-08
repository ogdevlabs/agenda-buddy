namespace AgendaBuddy.Provider.Domain.Commands;

[ExcludeFromCodeCoverage]
public class SetProviderConsentCommand : IRequest<Result<ProviderEntity>>
{
    public required string Email { get; set; }

    /// <summary>Whether the Terms and Conditions are accepted as of now. <c>false</c> clears any prior acceptance.</summary>
    public required bool AcceptedTerms { get; set; }

    /// <summary>Whether the Privacy Policy is accepted as of now. <c>false</c> clears any prior acceptance.</summary>
    public required bool AcceptedPrivacy { get; set; }
}
