using AgendaBuddy.Provider.Api.Showcase;

namespace AgendaBuddy.Provider.Tests.Showcase;

public class StoreRedirectTest
{
    private const string AppStore = "https://apps.apple.com/app/id123";
    private const string PlayStore = "https://play.google.com/store/apps/details?id=app.agendame";

    [Theory]
    [InlineData("Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Mobile/15E148 Safari/604.1")]
    [InlineData("Mozilla/5.0 (iPad; CPU OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Mobile/15E148")]
    [InlineData("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1")]
    public void Classify_AppleMobileDevices_AreIos(string userAgent) =>
        Assert.Equal(StoreRedirect.Ios, StoreRedirect.Classify(userAgent));

    [Theory]
    [InlineData("Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Mobile Safari/537.36")]
    [InlineData("Mozilla/5.0 (Linux; Android 13; SM-X700) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36")]
    public void Classify_Android_IsAndroid(string userAgent) =>
        Assert.Equal(StoreRedirect.Android, StoreRedirect.Classify(userAgent));

    [Theory]
    [InlineData("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Safari/605.1.15")]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36")]
    [InlineData("curl/8.4.0")]
    [InlineData("")]
    [InlineData(null)]
    public void Classify_EverythingElse_IsOther(string? userAgent) =>
        Assert.Equal(StoreRedirect.Other, StoreRedirect.Classify(userAgent));

    [Fact]
    public void ChooserPage_LinksBothHttpsStores()
    {
        var html = StoreRedirect.ChooserPage(AppStore, PlayStore);

        Assert.Contains("href=\"https://apps.apple.com/app/id123\"", html);
        Assert.Contains("href=\"https://play.google.com/store/apps/details?id=app.agendame\"", html);
    }

    [Theory]
    [InlineData("http://apps.apple.com/app/id123")]
    [InlineData("javascript:alert(1)")]
    [InlineData("//evil.example/app")]
    [InlineData("not a url")]
    [InlineData("")]
    [InlineData(null)]
    public void ChooserPage_NeverLinksANonHttpsUrl(string? url)
    {
        var html = StoreRedirect.ChooserPage(url, url);

        Assert.DoesNotContain("<a ", html);
        if (!string.IsNullOrEmpty(url))
            Assert.DoesNotContain(url, html);
    }

    [Fact]
    public void ChooserPage_EncodesTheConfiguredUrl()
    {
        var html = StoreRedirect.ChooserPage("https://apps.apple.com/app?a=1&b=\"><script>", null);

        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&amp;b=", html);
    }

    [Fact]
    public void ChooserPage_CarriesSpanishAndEnglishInstructions()
    {
        var html = StoreRedirect.ChooserPage(AppStore, PlayStore);

        Assert.Contains("Escanea para descargar AgendaMe", html);
        Assert.Contains("Scan to get AgendaMe", html);
        Assert.Contains("toca Escanear código", html);
        Assert.Contains("tap Scan a code", html);
    }

    [Fact]
    public void ChooserPage_IsTheSameBytesWhateverTheCode() =>
        Assert.Equal(StoreRedirect.ChooserPage(AppStore, PlayStore), StoreRedirect.ChooserPage(AppStore, PlayStore));

    [Theory]
    [InlineData("https://apps.apple.com/app/id123", true)]
    [InlineData("http://apps.apple.com/app/id123", false)]
    [InlineData("ftp://example.com", false)]
    [InlineData("/relative", false)]
    [InlineData(null, false)]
    public void IsRedirectable_OnlyAbsoluteHttps(string? url, bool expected) =>
        Assert.Equal(expected, StoreRedirect.IsRedirectable(url));
}
