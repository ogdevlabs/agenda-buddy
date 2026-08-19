namespace Library.Dtos;

/// <summary>
/// The paginated envelope both list endpoints return. <b>This is a published contract</b> — F-015 is written
/// against it (ADR-023, `api-contracts.md` §4).
/// </summary>
/// <param name="Items">The page. Empty array on a page past the end — <b>200 with `[]`, not 404</b>.</param>
/// <param name="TotalCount">
/// Total matching documents. <see cref="long"/> because <c>CountDocumentsAsync</c> returns
/// <see cref="long"/>; narrowing it would be a breaking change to a published contract.
/// </param>
/// <param name="Page">Echoed, post-clamp.</param>
/// <param name="PageSize">The <b>effective</b> size, post-clamp — not necessarily what was requested.</param>
/// <remarks>
/// <para>
/// ⚠️ <b>Breaking change:</b> both list endpoints previously returned a bare JSON array, and both returned
/// <c>204</c> for an empty collection. They now return this envelope with <c>200</c> and
/// <c>items: []</c> — a client always gets a parseable body. Safe only because no client can currently reach
/// these routes (`01-api-surface.md:158`); doing it after F-015 would mean writing the mobile client twice.
/// </para>
/// <para>
/// <b>Accepted debt with a named trigger:</b> <c>skip</c>/<c>limit</c> degrades linearly with offset.
/// Immaterial at current volumes (synthetic data only); the fix at scale is keyset pagination, which
/// <em>would change this contract</em>. Revisit <b>before</b> real user data lands, not after — by then F-015
/// depends on this shape.
/// </para>
/// </remarks>
public sealed record PagedResponse<T>(IEnumerable<T> Items, long TotalCount, int Page, int PageSize)
{
    /// <summary>Builds the envelope from a repository result and the clamped request that produced it.</summary>
    public static PagedResponse<T> From(IEnumerable<T> items, long totalCount, PageRequest page) =>
        new(items, totalCount, page.Page, page.PageSize);
}
