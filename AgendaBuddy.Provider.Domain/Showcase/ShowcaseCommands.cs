namespace AgendaBuddy.Provider.Domain.Showcase;

[ExcludeFromCodeCoverage]
public class SetShowcaseTextCommand : IRequest<Result<ShowcaseOwnerView>>
{
    public required string Email { get; set; }
    public string? Tagline { get; set; }
    public string? About { get; set; }
}

public enum ShowcaseImageSlot
{
    Photo,
    Logo
}

[ExcludeFromCodeCoverage]
public class SetShowcaseImageCommand : IRequest<Result<ShowcaseOwnerView>>
{
    public required string Email { get; set; }
    public required ShowcaseImageSlot Slot { get; set; }
    public string? Hash { get; set; }
}

public sealed record PortfolioWrite(PortfolioItemView Item, bool Created);

[ExcludeFromCodeCoverage]
public class AddPortfolioItemCommand : IRequest<Result<PortfolioWrite>>
{
    public required string Email { get; set; }
    public required string Hash { get; set; }
    public string? Caption { get; set; }
    public string? ServiceId { get; set; }
}

[ExcludeFromCodeCoverage]
public class UpdatePortfolioItemCommand : IRequest<Result<PortfolioItemView>>
{
    public required string Email { get; set; }
    public required string Hash { get; set; }
    public string? Caption { get; set; }
    public string? ServiceId { get; set; }
}

[ExcludeFromCodeCoverage]
public class RemovePortfolioItemCommand : IRequest<Result>
{
    public required string Email { get; set; }
    public required string Hash { get; set; }
}

[ExcludeFromCodeCoverage]
public class ReorderPortfolioCommand : IRequest<Result<ShowcaseOwnerView>>
{
    public required string Email { get; set; }
    public required List<string> Hashes { get; set; }
}

[ExcludeFromCodeCoverage]
public class CreatePublicCodeCommand : IRequest<Result<PublicCodeView>>
{
    public required string Email { get; set; }
}

/// <summary>An upload. The bytes are never written to the audit record — only their size and resulting hash.</summary>
[ExcludeFromCodeCoverage]
public class UploadMediaCommand : IRequest<Result<MediaUploadView>>
{
    public required string Email { get; set; }
    public required byte[] Content { get; set; }
}

[ExcludeFromCodeCoverage]
public class ReportShowcaseCommand : IRequest<Result>
{
    public required string ReporterEmail { get; set; }
    public required string ProviderRef { get; set; }
    public required string Reason { get; set; }
    public string? Detail { get; set; }
    public string? PortfolioHash { get; set; }
}

[ExcludeFromCodeCoverage]
public class SetShowcaseHiddenCommand : IRequest<Result>
{
    public required string CustomerEmail { get; set; }
    public required string ProviderRef { get; set; }
    public required bool Hidden { get; set; }
}
