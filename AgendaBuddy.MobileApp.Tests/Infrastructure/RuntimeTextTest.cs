using System.Globalization;
using AgendaBuddy.Library.Entities;
using AgendaBuddy.MobileApp.Resources.Strings;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Infrastructure;

[Collection(nameof(CultureSensitiveCollection))]
public class RuntimeTextTest : IDisposable
{
    private readonly CultureInfo? _originalCulture = AppResources.Culture;

    public void Dispose() => AppResources.Culture = _originalCulture;

    [Fact]
    public void EveryRuntimeEnumStateHasLocalizedEnglishAndSpanishText()
    {
        foreach (var cultureName in new[] { "en", "es-MX" })
        {
            AppResources.Culture = new CultureInfo(cultureName);

            Assert.All(Enum.GetValues<AppointmentStatus>(), status =>
                Assert.NotEqual(AppResources.GetString("AppointmentStatus_Unknown"), RuntimeText.AppointmentStatus(status)));
            Assert.All(Enum.GetValues<PaymentStatus>(), status =>
                Assert.False(string.IsNullOrWhiteSpace(PaymentText(status))));
            Assert.All(Enum.GetValues<FeeType>(), feeType =>
                Assert.NotEqual(AppResources.GetString("FeeType_Unknown"), RuntimeText.FeeType(feeType)));
        }
    }

    [Fact]
    public void SpanishFormattingUsesMexicanDatesCurrencyDurationsAndRelativeTime()
    {
        AppResources.Culture = new CultureInfo("es-MX");
        var date = new DateTime(2026, 9, 8, 14, 30, 0);

        Assert.Equal("martes", date.ToString("dddd", AppResources.CurrentCulture));
        Assert.Equal("$1,234.50", RuntimeText.Currency(1234.5m));
        Assert.Equal("1 min", RuntimeText.Duration(1));
        Assert.Equal("45 min", RuntimeText.Duration(45));
        Assert.Equal("hace 2 h", RuntimeText.RelativeTime(date, date.AddHours(2)));
    }

    [Fact]
    public void EnglishFormattingPreservesCurrentCompactLabels()
    {
        AppResources.Culture = new CultureInfo("en-US");
        var now = new DateTime(2026, 9, 8, 14, 30, 0);

        Assert.Equal("$1,234.50", RuntimeText.Currency(1234.5m));
        Assert.Equal("5m ago", RuntimeText.RelativeTime(now.AddMinutes(-5), now));
        Assert.Equal("now", RuntimeText.RelativeTime(now.AddSeconds(1), now));
    }

    private static string PaymentText(PaymentStatus status) => status switch
    {
        PaymentStatus.Succeeded => AppResources.Payment_Paid,
        PaymentStatus.Pending => AppResources.Payment_Pending,
        PaymentStatus.Failed => AppResources.Payment_Failed,
        PaymentStatus.Refunded => AppResources.Payment_Refunded,
        _ => AppResources.Payment_StatusUnknown
    };
}