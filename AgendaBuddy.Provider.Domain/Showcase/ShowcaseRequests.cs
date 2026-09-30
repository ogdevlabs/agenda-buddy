namespace AgendaBuddy.Provider.Domain.Showcase;

public sealed record ShowcaseTextRequest(string? Tagline, string? About);

public sealed record ShowcaseImageRequest(string? Hash);

public sealed record AddPortfolioItemRequest(string? Hash, string? Caption, string? ServiceId);

public sealed record UpdatePortfolioItemRequest(string? Caption, string? ServiceId);

public sealed record ReorderPortfolioRequest(List<string>? Hashes);

public sealed record ShowcaseLookupRequest(List<string>? Emails);

public sealed record ShowcaseReportRequest(string? Reason, string? Detail, string? PortfolioHash);
