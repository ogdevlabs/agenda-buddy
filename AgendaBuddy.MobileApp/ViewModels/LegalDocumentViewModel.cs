using CommunityToolkit.Mvvm.ComponentModel;
using AgendaBuddy.MobileApp.Infrastructure;

namespace AgendaBuddy.MobileApp.ViewModels;

/// <summary>
/// One legal document on screen — the Terms or the Privacy Policy.
/// </summary>
/// <remarks>
/// One view model for both documents, set by <see cref="Show"/>, because the two pages differ only in which
/// <see cref="LegalDocument"/> they render. Two near-identical view models would be two places for the layout to
/// drift.
/// </remarks>
public partial class LegalDocumentViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _effectiveDate = string.Empty;

    [ObservableProperty]
    private string _summary = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<LegalClause> _clauses = [];

    /// <summary>
    /// Where this document will be published once the site exists.
    /// </summary>
    /// <remarks>
    /// ⚠️ Not linked from the page yet, deliberately: neither URL resolves today, and a link to a 404 is worse
    /// than no link when the full text is already on the screen.
    /// </remarks>
    [ObservableProperty]
    private string _publishedUrl = string.Empty;

    public void Show(LegalDocument document, string publishedUrl)
    {
        Title = document.Title;
        EffectiveDate = document.EffectiveDateText;
        Summary = document.Summary;
        Clauses = document.Clauses;
        PublishedUrl = publishedUrl;
    }

    public void ShowTerms() => Show(LegalDocuments.Terms, LegalDocuments.TermsUrl);

    public void ShowPrivacy() => Show(LegalDocuments.Privacy, LegalDocuments.PrivacyUrl);
}
