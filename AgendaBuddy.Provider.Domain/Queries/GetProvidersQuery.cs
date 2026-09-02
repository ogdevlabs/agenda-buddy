namespace AgendaBuddy.Provider.Domain.Queries;

[ExcludeFromCodeCoverage]
public class GetProvidersQuery : IRequest<Result<PagedResponse<ProviderEntity>>>
{
    public required PageRequest Page { get; set; }

    /// <summary>
    /// Restrict the page to providers a customer could actually book — at least one active service
    /// classified under a profession.
    /// </summary>
    /// <remarks>
    /// <b>Opt-in, not the default.</b> It was briefly defaulted to <c>true</c> on the reasoning that this
    /// list IS the customer directory, which broke seven integration tests covering pagination, the
    /// non-owner projection, and query auditing. That was the right signal: this route's contract is a
    /// general paginated provider list with those guarantees, and silently narrowing it redefines what
    /// <c>totalCount</c> and "page 2" mean for every caller. The filter is a product concern the
    /// customer-facing client asks for explicitly (<c>ProviderRouteBuilder.Providers</c> sends
    /// <c>bookableOnly=true</c>), so the endpoint stays honest and the directory still never shows a
    /// provider it cannot book.
    /// </remarks>
    public bool BookableOnly { get; set; }
}
