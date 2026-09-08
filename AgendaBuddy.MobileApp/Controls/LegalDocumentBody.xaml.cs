#if MOBILE
namespace AgendaBuddy.MobileApp.Controls;

/// <summary>
/// Renders a legal document. Binds to <c>LegalDocumentViewModel</c>, which both legal pages set as their
/// BindingContext.
/// </summary>
public partial class LegalDocumentBody : ContentView
{
    public LegalDocumentBody() => InitializeComponent();
}
#endif
