#if MOBILE
using AgendaBuddy.MobileApp.ViewModels;

namespace AgendaBuddy.MobileApp.Views;

/// <summary>
/// The Terms and Conditions, in the app.
/// </summary>
/// <remarks>
/// Shares <see cref="LegalDocumentViewModel"/> and <c>LegalDocumentBody</c> with <see cref="PrivacyPage"/> — the
/// two pages differ only in which document they hand it, so a second layout would be a second place to drift.
/// </remarks>
public partial class TermsPage : ContentPage
{
    public TermsPage(LegalDocumentViewModel viewModel)
    {
        InitializeComponent();
        viewModel.ShowTerms();
        BindingContext = viewModel;
    }
}
#endif
