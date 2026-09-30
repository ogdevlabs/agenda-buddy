#if MOBILE
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.ViewModels;

namespace AgendaBuddy.MobileApp.Views;

/// <summary>Query keys: <c>tiles</c> (the portfolio, in order) and <c>index</c> (the tile that was tapped).</summary>
public partial class PortfolioViewerPage : ContentPage, IQueryAttributable
{
    private readonly PortfolioViewerViewModel _viewModel;

    public PortfolioViewerPage(PortfolioViewerViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        var tiles = query.TryGetValue("tiles", out var raw) && raw is IEnumerable<PortfolioTile> items
            ? items.ToList()
            : new List<PortfolioTile>();
        var index = query.TryGetValue("index", out var rawIndex) && rawIndex is int position ? position : 0;
        _viewModel.Open(tiles, index);
    }
}
#endif
