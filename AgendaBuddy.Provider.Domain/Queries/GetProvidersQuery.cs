namespace AgendaBuddy.Provider.Domain.Queries;

[ExcludeFromCodeCoverage]
public class GetProvidersQuery : IRequest<Result<PagedResponse<ProviderEntity>>>
{
    public required PageRequest Page { get; set; }

    /// <summary>
    /// Restrict the page to providers a customer could actually book — at least one active service
    /// classified under a profession. Defaults to <c>true</c>: this list is the customer-facing directory,
    /// and offering a provider who cannot be booked dead-ends the flow.
    /// </summary>
    public bool BookableOnly { get; set; } = true;
}
