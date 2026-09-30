#if MOBILE
namespace AgendaBuddy.MobileApp.Controls;

/// <summary>
/// The viewer's next booked session with this provider, when there is one. Hidden entirely otherwise, so a
/// customer who has never booked sees no empty band.
/// </summary>
public class NextSessionBanner : ContentView
{
    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(NextSessionBanner), string.Empty,
            propertyChanged: (b, _, n) => ((NextSessionBanner)b).Apply((string?)n));

    private readonly Label _label = new() { FontSize = 14, TextColor = Colors.White, LineBreakMode = LineBreakMode.WordWrap };

    public NextSessionBanner()
    {
        var background = Application.Current?.Resources.TryGetValue("Primary", out var primary) == true && primary is Color color
            ? color
            : Colors.DarkSlateBlue;
        Content = new Border
        {
            BackgroundColor = background,
            StrokeThickness = 0,
            Padding = new Thickness(14, 10),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
            Content = _label
        };
        IsVisible = false;
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    private void Apply(string? text)
    {
        _label.Text = text ?? string.Empty;
        IsVisible = !string.IsNullOrWhiteSpace(text);
        SemanticProperties.SetDescription(this, text ?? string.Empty);
    }
}
#endif
