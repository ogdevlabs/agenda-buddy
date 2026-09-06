#if MOBILE
using System.Globalization;

namespace AgendaBuddy.MobileApp.Infrastructure;

/// <summary>
/// Turns a hex string from a view model into a <see cref="Color"/> for XAML.
/// </summary>
/// <remarks>
/// The per-notification accents come from <see cref="NotificationVisuals"/> as strings, so that whole mapping
/// stays on the <c>net10.0</c> test slice — <see cref="Color"/> does not exist there. This is the one-line
/// adapter at the boundary. An unparseable or absent value falls back to <see cref="Fallback"/> rather than
/// throwing: a binding error inside a <c>CollectionView</c> template is silent, so throwing here would produce
/// rows that simply fail to draw.
/// </remarks>
public class HexColorConverter : IValueConverter
{
    /// <summary>Used when there is nothing to parse. Neutral, so a miss never looks like a real state.</summary>
    public static readonly Color Fallback = Color.FromArgb(NotificationVisuals.NeutralAccent);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string hex || string.IsNullOrWhiteSpace(hex))
            return Fallback;

        try
        {
            return Color.FromArgb(hex);
        }
        catch (Exception)
        {
            return Fallback;
        }
    }

    /// <summary>One-way. A colour on screen is never the source of truth for a notification's type.</summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException($"{nameof(HexColorConverter)} is one-way.");
}
#endif
