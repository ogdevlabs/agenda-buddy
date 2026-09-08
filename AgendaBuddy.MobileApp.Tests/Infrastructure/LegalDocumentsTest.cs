using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.ViewModels;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Infrastructure;

/// <summary>
/// The Terms and the Privacy Policy shipped in the app.
/// </summary>
/// <remarks>
/// These are minimal industry-standard clauses, not legal advice, and they need a lawyer's review before public
/// release. What is asserted here is structural: that both documents exist, that they are complete enough to be
/// read before the consent boxes are ticked, and that the privacy policy describes what this product actually does
/// rather than a generic list — a policy that claims things the app does not do is as much a defect as one that
/// omits things it does.
/// </remarks>
public class LegalDocumentsTest
{
    public static TheoryData<LegalDocument> BothDocuments() => new()
    {
        LegalDocuments.Terms,
        LegalDocuments.Privacy
    };

    [Theory]
    [MemberData(nameof(BothDocuments))]
    public void EachDocumentHasATitleASummaryAndAnEffectiveDate(LegalDocument document)
    {
        Assert.False(string.IsNullOrWhiteSpace(document.Title));
        Assert.False(string.IsNullOrWhiteSpace(document.Summary));
        Assert.Equal(LegalDocuments.EffectiveDate, document.EffectiveDate);
    }

    /// <summary>
    /// Every clause has to have both halves. A heading with no body renders as a bare line the reader cannot act
    /// on, and a body with no heading is unnavigable in a document people scan rather than read.
    /// </summary>
    [Theory]
    [MemberData(nameof(BothDocuments))]
    public void EveryClauseHasAHeadingAndABody(LegalDocument document)
    {
        Assert.NotEmpty(document.Clauses);
        Assert.All(document.Clauses, clause =>
        {
            Assert.False(string.IsNullOrWhiteSpace(clause.Heading));
            Assert.False(string.IsNullOrWhiteSpace(clause.Body));
        });
    }

    /// <summary>Both documents say who to write to, or a rights request has nowhere to go.</summary>
    [Theory]
    [MemberData(nameof(BothDocuments))]
    public void EachDocumentNamesAContactAddress(LegalDocument document)
    {
        Assert.Contains(
            LegalDocuments.ContactEmail,
            document.ToPlainText(),
            StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(BothDocuments))]
    public void EachDocumentCarriesTheProductsOwnName(LegalDocument document)
    {
        Assert.Contains(AppBrand.Name, document.ToPlainText(), StringComparison.Ordinal);
    }

    /// <summary>
    /// ⚠️ <b>The privacy policy has to describe the data this app really handles.</b> Every item below is
    /// something the code actually collects — an address, a name, a phone number, a device time zone and a push
    /// token — and a policy omitting one of them is a false statement, not a stylistic gap.
    /// </summary>
    [Theory]
    [InlineData("email")]
    [InlineData("phone")]
    [InlineData("time zone")]
    [InlineData("push")]
    [InlineData("appointment")]
    public void ThePrivacyPolicyNamesWhatTheAppActuallyCollects(string subject)
    {
        Assert.Contains(subject, LegalDocuments.Privacy.ToPlainText(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Deletion is the erasure right this app exercises directly, so the policy has to say what deleting does —
    /// including that the counterparty's records survive with the identity stripped, which is the part a reader
    /// would otherwise be surprised by.
    /// </summary>
    [Fact]
    public void ThePrivacyPolicyExplainsWhatAccountDeletionDoes()
    {
        var text = LegalDocuments.Privacy.ToPlainText();

        Assert.Contains("delete your account", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("strip", text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The email cannot be changed, and the policy is where that limitation belongs.</summary>
    [Fact]
    public void ThePrivacyPolicySaysTheEmailAddressCannotBeChanged()
    {
        Assert.Contains(
            "email address cannot be changed",
            LegalDocuments.Privacy.ToPlainText(),
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The terms have to say the app is a scheduling tool and not a party to the appointment — the single most
    /// load-bearing clause in a marketplace-shaped product.
    /// </summary>
    [Fact]
    public void TheTermsDisclaimBeingAPartyToTheAppointment()
    {
        Assert.Contains(
            "not a party to any appointment",
            LegalDocuments.Terms.ToPlainText(),
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// ⚠️ Both URLs point at a site that <b>does not exist yet</b>. They are declared so the App Store listing and
    /// a future "view online" link have one place to read them from; nothing links out to them today, which is why
    /// the in-app text is the canonical copy.
    /// </summary>
    [Fact]
    public void ThePublishedUrlsAreDeclaredForWhenTheSiteExists()
    {
        Assert.StartsWith("https://", LegalDocuments.TermsUrl, StringComparison.Ordinal);
        Assert.StartsWith("https://", LegalDocuments.PrivacyUrl, StringComparison.Ordinal);
        Assert.NotEqual(LegalDocuments.TermsUrl, LegalDocuments.PrivacyUrl);
    }

    // ── the view model both pages share ────────────────────────────────────────────────────────────

    [Fact]
    public void ShowingTheTermsBindsTheTermsAndItsUrl()
    {
        var vm = new LegalDocumentViewModel();

        vm.ShowTerms();

        Assert.Equal(LegalDocuments.Terms.Title, vm.Title);
        Assert.Equal(LegalDocuments.Terms.Clauses.Count, vm.Clauses.Count);
        Assert.Equal(LegalDocuments.TermsUrl, vm.PublishedUrl);
        Assert.Contains(LegalDocuments.EffectiveDate, vm.EffectiveDate, StringComparison.Ordinal);
    }

    [Fact]
    public void ShowingThePrivacyPolicyBindsThePolicyAndItsUrl()
    {
        var vm = new LegalDocumentViewModel();

        vm.ShowPrivacy();

        Assert.Equal(LegalDocuments.Privacy.Title, vm.Title);
        Assert.Equal(LegalDocuments.Privacy.Clauses.Count, vm.Clauses.Count);
        Assert.Equal(LegalDocuments.PrivacyUrl, vm.PublishedUrl);
    }

    /// <summary>
    /// One view model serves both pages, so switching between them must fully replace the content — a leftover
    /// clause from the other document would put the Terms inside the Privacy Policy.
    /// </summary>
    [Fact]
    public void SwitchingDocumentsReplacesTheContentEntirely()
    {
        var vm = new LegalDocumentViewModel();

        vm.ShowTerms();
        vm.ShowPrivacy();

        Assert.Equal(LegalDocuments.Privacy.Title, vm.Title);
        Assert.Equal(LegalDocuments.Privacy.Clauses, vm.Clauses);
    }
}
