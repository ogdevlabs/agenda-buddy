#if MOBILE
using AgendaBuddy.MobileApp.ViewModels;

namespace AgendaBuddy.MobileApp.Views;

/// <summary>The Privacy Policy, in the app. See <see cref="TermsPage"/> for why they share a view model.</summary>
public partial class PrivacyPage : ContentPage
{
    public PrivacyPage(LegalDocumentViewModel viewModel)
    {
        InitializeComponent();
        viewModel.ShowPrivacy();
        BindingContext = viewModel;
    }
}
#endif
