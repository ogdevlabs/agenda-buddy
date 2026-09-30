#if MOBILE
using System.Windows.Input;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Routing;

namespace AgendaBuddy.MobileApp.Controls;

/// <summary>
/// A row that leads from an appointment, a booking or a thread to the provider's showcase: their photo, up to
/// four portfolio thumbnails when the caller has them, and a "See {FirstName}'s work" chip.
/// </summary>
/// <remarks>
/// The lookup route names a provider's photo but not their portfolio, so on those screens the strip is the photo
/// and the chip; thumbnails appear only where a caller already holds the tiles.
/// </remarks>
public class ProviderWorkStrip : ContentView
{
    public const int MaxThumbnails = 4;

    public static readonly BindableProperty ProviderRefProperty =
        BindableProperty.Create(nameof(ProviderRef), typeof(string), typeof(ProviderWorkStrip), null,
            propertyChanged: (b, _, _) => ((ProviderWorkStrip)b).Rebuild());

    public static readonly BindableProperty PhotoHashProperty =
        BindableProperty.Create(nameof(PhotoHash), typeof(string), typeof(ProviderWorkStrip), null,
            propertyChanged: (b, _, _) => ((ProviderWorkStrip)b).Rebuild());

    public static readonly BindableProperty TilesProperty =
        BindableProperty.Create(nameof(Tiles), typeof(IEnumerable<PortfolioTile>), typeof(ProviderWorkStrip), null,
            propertyChanged: (b, _, _) => ((ProviderWorkStrip)b).Rebuild());

    public static readonly BindableProperty LabelProperty =
        BindableProperty.Create(nameof(Label), typeof(string), typeof(ProviderWorkStrip), string.Empty,
            propertyChanged: (b, _, _) => ((ProviderWorkStrip)b).Rebuild());

    public static readonly BindableProperty CommandProperty =
        BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(ProviderWorkStrip));

    private readonly HorizontalStackLayout _row = new() { Spacing = 8, VerticalOptions = LayoutOptions.Center };

    public ProviderWorkStrip()
    {
        Content = _row;
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => Open();
        GestureRecognizers.Add(tap);
    }

    public string? ProviderRef
    {
        get => (string?)GetValue(ProviderRefProperty);
        set => SetValue(ProviderRefProperty, value);
    }

    public string? PhotoHash
    {
        get => (string?)GetValue(PhotoHashProperty);
        set => SetValue(PhotoHashProperty, value);
    }

    public IEnumerable<PortfolioTile>? Tiles
    {
        get => (IEnumerable<PortfolioTile>?)GetValue(TilesProperty);
        set => SetValue(TilesProperty, value);
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    private void Open()
    {
        if (Command?.CanExecute(null) == true)
            Command.Execute(null);
    }

    private void Rebuild()
    {
        _row.Children.Clear();
        if (string.IsNullOrWhiteSpace(ProviderRef))
            return;

        if (!string.IsNullOrWhiteSpace(PhotoHash))
            _row.Add(new Avatar { Size = 40, ProviderRef = ProviderRef, PhotoHash = PhotoHash });

        foreach (var tile in Tiles?.Where(t => t.HasImage).Take(MaxThumbnails) ?? [])
        {
            _row.Add(new Border
            {
                WidthRequest = 40,
                HeightRequest = 40,
                StrokeThickness = 0,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 6 },
                Content = new AuthenticatedImage { ProviderRef = ProviderRef, Hash = tile.Hash, Variant = MediaVariant.Thumb }
            });
        }

        var chip = new Button
        {
            Text = Label,
            FontSize = 13,
            Padding = new Thickness(12, 4),
            HeightRequest = 34,
            CornerRadius = 17,
            VerticalOptions = LayoutOptions.Center
        };
        if (Application.Current?.Resources.TryGetValue("SecondaryButton", out var style) == true && style is Style s)
            chip.Style = s;
        chip.Clicked += (_, _) => Open();
        SemanticProperties.SetDescription(chip, Label);
        _row.Add(chip);
    }
}
#endif
