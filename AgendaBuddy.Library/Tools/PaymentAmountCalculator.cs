namespace AgendaBuddy.Library.Tools;

public static class PaymentAmountCalculator
{
    private static readonly HashSet<string> ZeroDecimalCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "bif", "clp", "djf", "gnf", "jpy", "kmf", "krw", "mga", "pyg", "rwf", "ugx", "vnd", "vuv",
        "xaf", "xof", "xpf"
    };

    private static readonly HashSet<string> ThreeDecimalCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "bhd", "jod", "kwd", "omr", "tnd"
    };

    public static long CalculateMinorUnits(ServiceEntity service)
    {
        ArgumentNullException.ThrowIfNull(service);

        var fee = service.Fee.GetValueOrDefault();
        if (fee < 0) throw new ArgumentOutOfRangeException(nameof(service), "Service fee cannot be negative.");

        var amount = service.FeeType switch
        {
            FeeType.Fixed => fee,
            FeeType.Hourly when service.DurationMinutes is > 0 => fee * service.DurationMinutes.Value / 60m,
            FeeType.Hourly => throw new InvalidOperationException("An hourly service requires a positive duration."),
            FeeType.Subscription => throw new InvalidOperationException(
                "Subscription services require a billing policy before they can be booked."),
            _ => throw new ArgumentOutOfRangeException(nameof(service), "Unsupported fee type.")
        };

        var exponent = CurrencyExponent(service.Currency);
        var scale = exponent switch
        {
            0 => 1m,
            3 => 1000m,
            _ => 100m
        };

        return checked((long)decimal.Round(amount * scale, 0, MidpointRounding.AwayFromZero));
    }

    private static int CurrencyExponent(string currency)
    {
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            throw new ArgumentException("Currency must be a three-letter ISO code.", nameof(currency));

        if (ZeroDecimalCurrencies.Contains(currency)) return 0;
        if (ThreeDecimalCurrencies.Contains(currency)) return 3;
        return 2;
    }
}
