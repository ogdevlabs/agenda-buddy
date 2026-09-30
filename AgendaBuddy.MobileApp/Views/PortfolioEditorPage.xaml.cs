#if MOBILE
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Resources.Strings;
using AgendaBuddy.MobileApp.ViewModels;

namespace AgendaBuddy.MobileApp.Views;

public partial class PortfolioEditorPage : ContentPage
{
    private readonly PortfolioEditorViewModel _viewModel;

    public PortfolioEditorPage(PortfolioEditorViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
        _viewModel.MenuRequested += OnMenuRequested;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadCommand.Execute(null);
    }

    private async void OnMenuRequested(object? sender, PortfolioTile tile)
    {
        var actions = _viewModel.ActionsFor(tile);
        var labels = actions.Select(PortfolioEditorViewModel.LabelFor).ToArray();
        var cancel = AppResources.GetString("General_Cancel");
        var chosen = await DisplayActionSheet(AppResources.GetString("Portfolio_MoreOptions"), cancel, null, labels);
        var index = Array.IndexOf(labels, chosen);
        if (index < 0)
            return;

        var action = actions[index];
        if (action == PortfolioTileAction.EditCaption)
        {
            var caption = await DisplayPromptAsync(
                AppResources.GetString("Portfolio_EditCaption"),
                AppResources.GetString("Portfolio_CaptionPrompt"),
                AppResources.GetString("General_Save"),
                cancel,
                AppResources.GetString("Portfolio_CaptionPlaceholder"),
                -1,
                Keyboard.Text,
                tile.Caption ?? string.Empty);
            if (caption is not null)
                await _viewModel.SaveCaptionAsync(tile, caption);
            return;
        }

        await _viewModel.ApplyAsync(tile, action);
    }
}
#endif
