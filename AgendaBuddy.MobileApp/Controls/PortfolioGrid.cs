#if MOBILE
using System.Collections.Specialized;
using System.Windows.Input;
using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Routing;

namespace AgendaBuddy.MobileApp.Controls;

/// <summary>
/// A portfolio as three square columns, each tile marked NEW when it was added since the viewer last looked.
/// </summary>
/// <remarks>
/// A plain grid rather than a <c>CollectionView</c>: a portfolio is bounded at twenty images, so there is nothing
/// to virtualise, and a grid can sit inside a page's scroller where a <c>CollectionView</c> would be given
/// unbounded height and render everything anyway.
/// </remarks>
public class PortfolioGrid : ContentView
{
    public const int Columns = 3;

    public static readonly BindableProperty ItemsSourceProperty =
        BindableProperty.Create(nameof(ItemsSource), typeof(IEnumerable<PortfolioTile>), typeof(PortfolioGrid), null,
            propertyChanged: OnItemsSourceChanged);

    /// <summary>Executed with the tapped <see cref="PortfolioTile"/>.</summary>
    public static readonly BindableProperty TileCommandProperty =
        BindableProperty.Create(nameof(TileCommand), typeof(ICommand), typeof(PortfolioGrid));

    public static readonly BindableProperty MaxItemsProperty =
        BindableProperty.Create(nameof(MaxItems), typeof(int), typeof(PortfolioGrid), ShowcaseText.PortfolioCapacity,
            propertyChanged: (b, _, _) => ((PortfolioGrid)b).Rebuild());

    private readonly Grid _grid = new() { ColumnSpacing = 4, RowSpacing = 4 };
    private double _builtForWidth = -1;

    public PortfolioGrid()
    {
        for (var i = 0; i < Columns; i++)
            _grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        Content = _grid;
        SizeChanged += (_, _) =>
        {
            if (Math.Abs(Width - _builtForWidth) > 0.5)
                Rebuild();
        };
    }

    public IEnumerable<PortfolioTile>? ItemsSource
    {
        get => (IEnumerable<PortfolioTile>?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public ICommand? TileCommand
    {
        get => (ICommand?)GetValue(TileCommandProperty);
        set => SetValue(TileCommandProperty, value);
    }

    public int MaxItems
    {
        get => (int)GetValue(MaxItemsProperty);
        set => SetValue(MaxItemsProperty, value);
    }

    private static void OnItemsSourceChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        var grid = (PortfolioGrid)bindable;
        if (oldValue is INotifyCollectionChanged oldCollection)
            oldCollection.CollectionChanged -= grid.OnCollectionChanged;
        if (newValue is INotifyCollectionChanged newCollection)
            newCollection.CollectionChanged += grid.OnCollectionChanged;
        grid.Rebuild();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    private void Rebuild()
    {
        _builtForWidth = Width;
        _grid.Children.Clear();
        _grid.RowDefinitions.Clear();
        var tiles = ItemsSource?.Take(Math.Max(0, MaxItems)).ToList() ?? new List<PortfolioTile>();
        var edge = Width > 0 ? (Width - (Columns - 1) * _grid.ColumnSpacing) / Columns : 110;
        var rows = (tiles.Count + Columns - 1) / Columns;
        for (var r = 0; r < rows; r++)
            _grid.RowDefinitions.Add(new RowDefinition(new GridLength(edge)));

        for (var i = 0; i < tiles.Count; i++)
        {
            var cell = Tile(tiles[i]);
            _grid.Add(cell, i % Columns, i / Columns);
        }
    }

    private View Tile(PortfolioTile tile)
    {
        var image = new AuthenticatedImage
        {
            ProviderRef = tile.ProviderRef,
            Hash = tile.Hash,
            Variant = MediaVariant.Thumb
        };

        var cell = new Grid { BackgroundColor = ResourceColor(Brand.Divider) };
        cell.Add(image);
        if (tile.IsNew)
        {
            cell.Add(new Border
            {
                BackgroundColor = ResourceColor(Brand.Primary),
                StrokeThickness = 0,
                Padding = new Thickness(6, 2),
                Margin = new Thickness(4),
                HorizontalOptions = LayoutOptions.Start,
                VerticalOptions = LayoutOptions.Start,
                Content = new Label
                {
                    Text = AppResources.GetString("Portfolio_NewBadge"),
                    TextColor = Colors.White,
                    FontSize = 10,
                    FontAttributes = FontAttributes.Bold
                }
            });
        }

        SemanticProperties.SetDescription(cell, tile.Description);
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) =>
        {
            if (TileCommand?.CanExecute(tile) == true)
                TileCommand.Execute(tile);
        };
        cell.GestureRecognizers.Add(tap);
        return cell;
    }

    private static Color ResourceColor(string key) =>
        Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color
            ? color
            : Colors.Gray;

    private static class Brand
    {
        public const string Primary = "Primary";
        public const string Divider = "Divider";
    }
}
#endif
