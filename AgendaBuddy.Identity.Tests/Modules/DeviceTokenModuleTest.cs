using AgendaBuddy.Identity.Modules;
using Xunit;

namespace AgendaBuddy.Identity.Tests.Modules;

public class DeviceTokenModuleTest
{
    [Theory]
    [InlineData(null, "en")]
    [InlineData("", "en")]
    [InlineData("en", "en")]
    [InlineData("en-US", "en")]
    [InlineData("es", "es-MX")]
    [InlineData("ES-mx", "es-MX")]
    [InlineData("es-ES", "es-MX")]
    [InlineData(" es-MX ", "es-MX")]
    [InlineData("espresso", "en")]
    [InlineData("fr-FR", "en")]
    public void NormalizeLanguageCode_ReturnsSupportedLocaleOrEnglish(
        string? languageCode,
        string expected)
    {
        Assert.Equal(expected, DeviceTokenModule.NormalizeLanguageCode(languageCode));
    }
}
