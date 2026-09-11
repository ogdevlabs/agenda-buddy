using System.Xml.Linq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Views;

public class PaymentOnboardingEscapeTest
{
    [Theory]
    [InlineData("CustomerPaymentMethodPage.xaml", "SkipPaymentSetupButton")]
    [InlineData("ProviderPayoutPage.xaml", "SkipPayoutSetupButton")]
    public void OnboardingPageOffersAConditionalSkipAction(string view, string automationId)
    {
        var root = XDocument.Load(Path.Combine(RepoRoot(), "AgendaBuddy.MobileApp", "Views", view)).Root;
        Assert.NotNull(root);

        var button = root!.Descendants()
            .SingleOrDefault(element => element.Attribute("AutomationId")?.Value == automationId);

        Assert.NotNull(button);
        Assert.Equal("{Binding SkipOnboardingCommand}", button!.Attribute("Command")?.Value);
        Assert.Equal("{Binding ShowSkip}", button.Attribute("IsVisible")?.Value);
    }

    private static string RepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "agenda-buddy.sln")))
            current = current.Parent;

        return current?.FullName ?? throw new InvalidOperationException("Could not locate repository root.");
    }
}
