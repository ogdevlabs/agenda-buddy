namespace AgendaBuddy.Provider.Core.Showcase;

public static class ShowcaseProjection
{
    public static async Task<ShowcaseOwnerView> OwnerViewAsync(
        IShowcaseService showcaseService, ProviderEntity provider, ProviderShowcaseEntity showcase) =>
        new(
            provider.Id.ToString(),
            showcase.PublicCode,
            showcase.Tagline,
            showcase.About,
            showcase.PhotoHash,
            showcase.LogoHash,
            showcase.Portfolio.Select(PortfolioItemView.From).ToList(),
            showcaseService.Completeness(showcase),
            await showcaseService.GetFunnelAsync(provider, showcase),
            showcase.HiddenByOperator == true);

    /// <summary>Only what a customer can book: active and classified under a profession.</summary>
    public static List<ShowcaseServiceView> BookableServices(ProviderEntity provider) =>
        (provider.ServiceEntities ?? [])
            .Where(s => s.IsActive && !string.IsNullOrWhiteSpace(s.ProfessionName))
            .Select(s => new ShowcaseServiceView(
                s.Id.ToString(), s.Name, s.Fee ?? 0m, s.FeeType.ToString(), s.DurationMinutes))
            .ToList();

    /// <summary>Validates an optional service link's shape; the service itself checks it is the provider's.</summary>
    public static bool IsWellFormedServiceId(string? serviceId) =>
        serviceId is null || ObjectId.TryParse(serviceId, out _);

    public static Event Audit(string type, string status, object data) => new()
    {
        Id = ObjectId.GenerateNewId(),
        TimeStamp = DateTime.UtcNow,
        Status = status,
        Type = type,
        Data = JsonSerializer.Serialize(data)
    };
}
