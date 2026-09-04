namespace AgendaBuddy.MobileApp.Models;

/// <summary>
/// One line of the professions catalog: either a collapsible letter header or a profession under it.
/// </summary>
/// <remarks>
/// The catalog is one flat row list rather than a grouped <c>CollectionView</c>, because collapsing a
/// grouped list means emptying its groups, and a group with no items does not reliably render its own
/// header. Rebuilding the row list on each toggle sidesteps that, and is cheap: collapsed, the whole
/// catalog is about two dozen rows.
/// </remarks>
public sealed class ProfessionCatalogRow
{
    private ProfessionCatalogRow()
    {
    }

    /// <summary>The letter this row belongs to. Set on headers and on professions alike.</summary>
    public string Letter { get; private init; } = string.Empty;

    /// <summary>Null on a header row.</summary>
    public ProfessionItem? Profession { get; private init; }

    public bool IsHeader => Profession is null;

    public bool IsProfession => Profession is not null;

    /// <summary>How many professions the letter holds, shown on the header whether it is open or not.</summary>
    public int MemberCount { get; private init; }

    public bool IsExpanded { get; private init; }

    /// <summary>Open/closed affordance on the header. A glyph rather than an image, to stay font-scalable.</summary>
    public string Chevron => IsExpanded ? "▾" : "▸";

    public static ProfessionCatalogRow ForHeader(string letter, int memberCount, bool isExpanded) =>
        new() { Letter = letter, MemberCount = memberCount, IsExpanded = isExpanded };

    public static ProfessionCatalogRow ForProfession(string letter, ProfessionItem profession) =>
        new() { Letter = letter, Profession = profession };
}
