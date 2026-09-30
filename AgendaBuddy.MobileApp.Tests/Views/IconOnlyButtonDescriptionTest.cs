using System.Globalization;
using System.Xml.Linq;
using AgendaBuddy.MobileApp.Resources.Strings;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Views;

/// <summary>
/// A button whose face is a glyph (a QR mark, a share arrow, a <c>⋯</c>) has nothing for VoiceOver or TalkBack to
/// read except the glyph's Unicode name, so it must say what it does through <c>SemanticProperties.Description</c>.
/// </summary>
[Collection(nameof(Infrastructure.CultureSensitiveCollection))]
public class IconOnlyButtonDescriptionTest
{
    public static TheoryData<string> ShowcaseViews => new()
    {
        "MyShowcasePage.xaml",
        "ShowcasePhotoLogoPage.xaml",
        "ShowcaseTextPage.xaml",
        "PortfolioEditorPage.xaml",
        "ShowcaseSharePage.xaml",
        "ScanProviderPage.xaml",
        "ProviderShowcasePage.xaml",
        "PortfolioViewerPage.xaml",
        "HiddenProvidersPage.xaml",
    };

    [Theory]
    [MemberData(nameof(ShowcaseViews))]
    public void EveryIconOnlyButtonIsDescribed(string view)
    {
        var root = XDocument.Load(Path.Combine(ViewsDirectory(), view)).Root!;

        var undescribed = root.Descendants()
            .Where(IsButton)
            .Where(IsIconOnly)
            .Where(button => string.IsNullOrWhiteSpace(button.Attribute("SemanticProperties.Description")?.Value))
            .Select(button => button.Attribute("AutomationId")?.Value ?? button.Attribute("Text")?.Value ?? button.Name.LocalName)
            .ToList();

        Assert.True(undescribed.Count == 0,
            $"{view} has icon-only buttons with no SemanticProperties.Description: {string.Join(", ", undescribed)}");
    }

    /// <summary>The three icon-only buttons the design names, with the words it chose for them.</summary>
    [Theory]
    [InlineData("MyShowcasePage.xaml", "ShowcaseShowQrButton", "MyShowcase_ShowQr", "Show my QR code")]
    [InlineData("ShowcaseSharePage.xaml", "ShareLinkButton", "Share_Share", "Share")]
    [InlineData("ProviderShowcasePage.xaml", "ShowcaseMoreOptionsButton", "ProviderShowcase_MoreOptions", "More options")]
    public void TheNamedIconButtonsSayWhatTheyDo(string view, string automationId, string key, string english)
    {
        var root = XDocument.Load(Path.Combine(ViewsDirectory(), view)).Root!;
        var button = root.Descendants().Single(e => e.Attribute("AutomationId")?.Value == automationId);

        Assert.Equal($"{{x:Static strings:AppResources.{key}}}", button.Attribute("SemanticProperties.Description")?.Value);

        var original = AppResources.Culture;
        try
        {
            AppResources.Culture = new CultureInfo("en");
            Assert.Equal(english, AppResources.GetString(key));
        }
        finally
        {
            AppResources.Culture = original;
        }
    }

    [Fact]
    public void ThePortfolioTileMenuIsDescribed()
    {
        var root = XDocument.Load(Path.Combine(ViewsDirectory(), "PortfolioEditorPage.xaml")).Root!;

        Assert.Contains(root.Descendants().Where(IsButton),
            button => button.Attribute("SemanticProperties.Description")?.Value
                == "{x:Static strings:AppResources.Portfolio_MoreOptions}");
    }

    private static bool IsButton(XElement element) =>
        element.Name.LocalName is "Button" or "ImageButton" or "SafeButton";

    /// <summary>
    /// An <c>ImageButton</c>, a button with no text, or one whose literal text holds no letter or digit. A bound
    /// text is assumed to be words.
    /// </summary>
    private static bool IsIconOnly(XElement button)
    {
        if (button.Name.LocalName == "ImageButton")
            return true;

        var text = button.Attribute("Text")?.Value;
        if (string.IsNullOrWhiteSpace(text))
            return true;
        if (text.StartsWith('{'))
            return false;

        return !text.Any(char.IsLetterOrDigit);
    }

    private static string ViewsDirectory()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "agenda-buddy.sln")))
            current = current.Parent;

        if (current is null)
            throw new InvalidOperationException($"Could not locate repo root walking up from {AppContext.BaseDirectory}.");

        return Path.Combine(current.FullName, "AgendaBuddy.MobileApp", "Views");
    }
}
