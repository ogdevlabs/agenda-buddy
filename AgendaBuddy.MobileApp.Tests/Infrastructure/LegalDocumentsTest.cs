using System.Globalization;
using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Resources.Strings;
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
[Collection(nameof(CultureSensitiveCollection))]
public class LegalDocumentsTest : IDisposable
{
    private readonly CultureInfo? _originalCulture = AppResources.Culture;

    public LegalDocumentsTest() => AppResources.Culture = new CultureInfo("en-US");

    public void Dispose() => AppResources.Culture = _originalCulture;

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
        Assert.False(string.IsNullOrWhiteSpace(document.EffectiveDateText));
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("es-MX")]
    public void BothCulturesHaveTenCompleteOrderedClausesWithTheSameStructure(string cultureName)
    {
        AppResources.Culture = new CultureInfo(cultureName);

        foreach (var document in new[] { LegalDocuments.Terms, LegalDocuments.Privacy })
        {
            Assert.Equal(10, document.Clauses.Count);
            Assert.Equal(
                Enumerable.Range(1, 10).Select(number => $"{number}."),
                document.Clauses.Select(clause => clause.Heading.Split(' ', 2)[0]));
            Assert.All(document.Clauses, clause =>
            {
                Assert.False(string.IsNullOrWhiteSpace(clause.Heading));
                Assert.False(string.IsNullOrWhiteSpace(clause.Body));
            });
        }
    }

    [Fact]
    public void EnglishAndSpanishDocumentsHaveIdenticalOrderedStructure()
    {
        var english = DocumentsFor("en-US");
        var spanish = DocumentsFor("es-MX");

        Assert.Equal(english.Select(document => document.Clauses.Count), spanish.Select(document => document.Clauses.Count));
        Assert.Equal(
            english.SelectMany(document => document.Clauses).Select(ClauseNumber),
            spanish.SelectMany(document => document.Clauses).Select(ClauseNumber));
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

    [Theory]
    [InlineData("correo electrónico")]
    [InlineData("nombre")]
    [InlineData("teléfono")]
    [InlineData("servicios")]
    [InlineData("disponibilidad")]
    [InlineData("citas")]
    [InlineData("mensajes")]
    [InlineData("notas")]
    [InlineData("zona horaria")]
    [InlineData("token")]
    [InlineData("ubicación")]
    [InlineData("contactos")]
    [InlineData("fotos")]
    [InlineData("publicidad")]
    [InlineData("alojamiento en la nube")]
    [InlineData("bases de datos")]
    [InlineData("procesador de pagos")]
    [InlineData("auditoría")]
    [InlineData("eliminar tu cuenta")]
    [InlineData("menores de edad")]
    public void SpanishPrivacyPolicyPreservesEveryRequiredDisclosure(string subject)
    {
        AppResources.Culture = new CultureInfo("es-MX");

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

    [Fact]
    public void EffectiveDateAndVersionStayAlignedAcrossBothDocuments()
    {
        Assert.Equal(
            LegalDocuments.Version,
            LegalDocuments.EffectiveDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        foreach (var cultureName in new[] { "en-US", "es-MX" })
        {
            var documents = DocumentsFor(cultureName);
            Assert.All(documents, document => Assert.Equal(LegalDocuments.EffectiveDate, document.EffectiveDate));
            Assert.Single(documents.Select(document => document.EffectiveDateText).Distinct());
        }
    }

    [Fact]
    public void ToPlainTextUsesTheSelectedCulture()
    {
        var english = DocumentsFor("en-US")[0].ToPlainText();
        var spanish = DocumentsFor("es-MX")[0].ToPlainText();

        Assert.StartsWith("Terms and Conditions", english, StringComparison.Ordinal);
        Assert.Contains("Effective Tuesday, September 8, 2026", english, StringComparison.Ordinal);
        Assert.StartsWith("Términos y condiciones", spanish, StringComparison.Ordinal);
        Assert.Contains("Vigente desde el martes, 8 de septiembre de 2026", spanish, StringComparison.Ordinal);
        Assert.NotEqual(english, spanish);
    }

    [Fact]
    public void CultureSwitchReconstructsDocumentsInsteadOfCachingEnglishInstances()
    {
        AppResources.Culture = new CultureInfo("en-US");
        var englishTerms = LegalDocuments.Terms;
        var englishPrivacy = LegalDocuments.Privacy;

        AppResources.Culture = new CultureInfo("es-MX");
        var spanishTerms = LegalDocuments.Terms;
        var spanishPrivacy = LegalDocuments.Privacy;

        Assert.NotSame(englishTerms, spanishTerms);
        Assert.NotSame(englishPrivacy, spanishPrivacy);
        Assert.NotEqual(englishTerms.Title, spanishTerms.Title);
        Assert.NotEqual(englishPrivacy.Title, spanishPrivacy.Title);
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
        Assert.Equal(LegalDocuments.Terms.EffectiveDateText, vm.EffectiveDate);
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

    private static LegalDocument[] DocumentsFor(string cultureName)
    {
        AppResources.Culture = new CultureInfo(cultureName);
        return [LegalDocuments.Terms, LegalDocuments.Privacy];
    }

    private static string ClauseNumber(LegalClause clause) => clause.Heading.Split(' ', 2)[0];
}
