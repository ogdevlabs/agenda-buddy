namespace AgendaBuddy.Library.Dtos;

/// <summary>
/// A validated, clamped page request. Construct with <see cref="Clamp"/>.
/// </summary>
/// <param name="Page">1-based page number, already clamped.</param>
/// <param name="PageSize">The <b>effective</b> page size, already clamped to <see cref="MaxPageSize"/>.</param>
/// <remarks>
/// <para>
/// AC-15 / ADR-023. Pure, so the rule is tested once rather than duplicated inline in two
/// endpoints — and so the cap cannot drift between them.
/// </para>
/// <para>
/// <b><see cref="MaxPageSize"/> is a security control, not ergonomics.</b> An uncapped page size restores
/// exactly the full-dataset dump this feature exists to remove, so it is enforced server-side on the
/// untrusted value.
/// </para>
/// <para>
/// <b>Clamped, never rejected.</b> Rejecting with a 400 would tell an attacker the exact boundary and leave
/// an honest client no way to discover the cap; clamping and echoing the effective value lets a well-behaved
/// client detect it and paginate correctly. ⚠️ So <c>pageSize</c> in the response is the value actually
/// applied, not the value requested.
/// </para>
/// </remarks>
public readonly record struct PageRequest(int Page, int PageSize)
{
    /// <summary>Largest page size any caller can obtain. Published via `api-contracts.md` §4.</summary>
    public const int MaxPageSize = 100;

    /// <summary>Page size used when none is supplied, or when one below 1 is.</summary>
    public const int DefaultPageSize = 25;

    /// <summary>Documents to skip. Never negative.</summary>
    public int Skip => (Page - 1) * PageSize;

    /// <summary>
    /// Clamps caller-supplied values into the documented contract.
    /// </summary>
    public static PageRequest Clamp(int? page, int? pageSize)
    {
        var effectivePage = page is null or < 1 ? 1 : page.Value;
        var effectiveSize = pageSize switch
        {
            null or < 1 => DefaultPageSize,
            > MaxPageSize => MaxPageSize,
            _ => pageSize.Value,
        };

        // (page - 1) * pageSize overflows to a NEGATIVE skip for a large page, and a negative skip is what
        // the Mongo driver rejects -- a 500 on an attacker-controlled input. Bound the page so the product
        // cannot overflow, rather than trusting the multiplication.
        var maxPage = (int.MaxValue / effectiveSize) + 1;
        if (effectivePage > maxPage)
        {
            effectivePage = maxPage;
        }

        return new PageRequest(effectivePage, effectiveSize);
    }
}
