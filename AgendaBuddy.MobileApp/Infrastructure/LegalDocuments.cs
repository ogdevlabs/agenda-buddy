using AgendaBuddy.MobileApp.Resources.Strings;

namespace AgendaBuddy.MobileApp.Infrastructure;

/// <summary>
/// One clause of a legal document: a heading and its body.
/// </summary>
public record LegalClause(string Heading, string Body);

/// <summary>
/// A legal document — its title, its effective date, and its clauses.
/// </summary>
public record LegalDocument(
    string Title,
    DateOnly EffectiveDate,
    string EffectiveDateText,
    string Summary,
    IReadOnlyList<LegalClause> Clauses)
{
    /// <summary>The document as one plain-text block, for a share sheet or a copy action.</summary>
    public string ToPlainText() =>
        string.Join(
            Environment.NewLine + Environment.NewLine,
            new[] { Title, EffectiveDateText, Summary }
                .Concat(Clauses.Select(clause => $"{clause.Heading}{Environment.NewLine}{clause.Body}")));
}

/// <summary>
/// The Terms and Conditions and the Privacy Policy, as text this build ships.
/// </summary>
/// <remarks>
/// <para>
/// <b>In the app rather than only on a website, and that is the point.</b> App Review requires the text to be
/// reachable, and a link is only reachable when there is a network and the site exists. Shipping the text means
/// the consent checkboxes on the profile editor can always be read before they are ticked — a checkbox next to an
/// unreachable link is a consent nobody gave.
/// </para>
/// <para>
/// ⚠️ <b>These are minimal, industry-standard clauses, not legal advice, and they are not a substitute for a
/// lawyer's review before public release.</b> They describe what this product actually does — schedules
/// appointments between a provider and a customer, stores names, addresses, phone numbers and appointment
/// records, sends email and push — and deliberately claim nothing it does not do. The App Store listing
/// separately needs a hosted privacy URL (agenda-buddy-1hk.14); <see cref="TermsUrl"/> and
/// <see cref="PrivacyUrl"/> are where that goes once the site exists.
/// </para>
/// <para>
/// Free of MAUI types, so both documents' structure and completeness are covered on the <c>net10.0</c> test slice
/// — the same reason <c>NotificationVisuals</c> holds plain hex strings.
/// </para>
/// </remarks>
public static class LegalDocuments
{
    /// <summary>
    /// The date both documents took effect. One constant, because they were written together and a screen showing
    /// two different dates invites the question of which one is current.
    /// </summary>
    public static readonly DateOnly EffectiveDate = new(2026, 9, 9);

    public const string Version = "2026-09-09";

    /// <summary>
    /// Where the Terms will be published. ⚠️ <b>The site does not exist yet</b> — the in-app document is the
    /// canonical copy until it does, which is why nothing links out to this today.
    /// </summary>
    public const string TermsUrl = "https://fererelabs.com/agendame/terms";

    /// <summary>
    /// Where the Privacy Policy will be published. Same caveat as <see cref="TermsUrl"/>, and this is the URL the
    /// App Store listing will need.
    /// </summary>
    public const string PrivacyUrl = "https://fererelabs.com/agendame/privacy";

    /// <summary>Who to contact about either document.</summary>
    public const string ContactEmail = "AgendaMe@fererelabs.com";

    public static LegalDocument Terms => BuildDocument("Legal_Terms");

    public static LegalDocument Privacy => BuildDocument("Legal_Privacy");

    private static LegalDocument BuildDocument(string prefix) => new(
        Title: AppResources.GetString($"{prefix}_Title"),
        EffectiveDate: EffectiveDate,
        EffectiveDateText: AppResources.Format("Legal_EffectiveDateFormat", EffectiveDate),
        Summary: AppResources.Format($"{prefix}_Summary", AppBrand.Name),
        Clauses: Enumerable.Range(1, 10)
            .Select(number => new LegalClause(
                AppResources.GetString($"{prefix}_Clause{number:00}_Heading"),
                AppResources.Format($"{prefix}_Clause{number:00}_Body", AppBrand.Name, ContactEmail)))
            .ToArray());
}
