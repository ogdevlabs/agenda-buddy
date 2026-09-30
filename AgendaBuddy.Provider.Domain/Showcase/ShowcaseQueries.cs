using AgendaBuddy.Library.Showcase;

namespace AgendaBuddy.Provider.Domain.Showcase;

[ExcludeFromCodeCoverage]
public class GetMyShowcaseQuery : IRequest<Result<ShowcaseOwnerView>>
{
    public required string Email { get; set; }
}

/// <summary>Exactly one of <see cref="ProviderRef"/> and <see cref="Code"/> is set.</summary>
[ExcludeFromCodeCoverage]
public class GetShowcaseQuery : IRequest<Result<ShowcaseView>>
{
    public required string CallerEmail { get; set; }
    public string? ProviderRef { get; set; }
    public string? Code { get; set; }
    public string? Source { get; set; }
}

[ExcludeFromCodeCoverage]
public class LookupShowcaseAvatarsQuery : IRequest<Result<IReadOnlyList<ShowcaseAvatarLookup>>>
{
    public required string CallerEmail { get; set; }
    public required bool CallerIsProvider { get; set; }
    public required List<string> Emails { get; set; }
}

[ExcludeFromCodeCoverage]
public class GetHiddenProvidersQuery : IRequest<Result<IReadOnlyList<HiddenProvider>>>
{
    public required string CustomerEmail { get; set; }
}
