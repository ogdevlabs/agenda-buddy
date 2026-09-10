using AgendaBuddy.Library.Entities;
using AgendaBuddy.Library.Tools;
using Xunit;

namespace AgendaBuddy.Library.Tests.Tools;

public class PaymentAmountCalculatorTest
{
    [Fact]
    public void FixedFee_UsesCurrencyMinorUnits()
    {
        var service = new ServiceEntity("Consultation", "", 49.95m)
        {
            FeeType = FeeType.Fixed,
            Currency = "usd"
        };

        Assert.Equal(4995, PaymentAmountCalculator.CalculateMinorUnits(service));
    }

    [Fact]
    public void HourlyFee_UsesAuthoritativeDurationAndRoundsOnce()
    {
        var service = new ServiceEntity("Coaching", "", 100m)
        {
            FeeType = FeeType.Hourly,
            DurationMinutes = 45,
            Currency = "usd"
        };

        Assert.Equal(7500, PaymentAmountCalculator.CalculateMinorUnits(service));
    }

    [Fact]
    public void ZeroDecimalCurrency_DoesNotMultiplyByOneHundred()
    {
        var service = new ServiceEntity("Lesson", "", 2500m)
        {
            FeeType = FeeType.Fixed,
            Currency = "jpy"
        };

        Assert.Equal(2500, PaymentAmountCalculator.CalculateMinorUnits(service));
    }

    [Fact]
    public void Subscription_RequiresSeparateBillingPolicy()
    {
        var service = new ServiceEntity("Membership", "", 20m)
        {
            FeeType = FeeType.Subscription
        };

        Assert.Throws<InvalidOperationException>(() => PaymentAmountCalculator.CalculateMinorUnits(service));
    }
}
