using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Resources.Strings;
using AgendaBuddy.MobileApp.Services;
using AgendaBuddy.MobileApp.ViewModels;
using Moq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.ViewModels;

public class PortfolioEditorViewModelTests
{
    private readonly Mock<IShowcaseApiService> _api = new();
    private readonly Mock<IImagePicker> _picker = new();
    private readonly Mock<IInAppAlertService> _alerts = new();

    private PortfolioEditorViewModel Create(int stored)
    {
        _api.Setup(a => a.GetMineAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ShowcaseFixtures.Ok(ShowcaseFixtures.Mine(portfolio: stored)));
        return new PortfolioEditorViewModel(_api.Object, _picker.Object, _alerts.Object);
    }

    private static MyShowcase WithOrder(params string[] hashes) => new()
    {
        ProviderRef = "ref-1",
        Portfolio = hashes.Select(h => new PortfolioItem { Hash = h }).ToList()
    };

    [Fact]
    public async Task TheCountAndPickerLimitFollowWhatIsStored()
    {
        var vm = Create(18);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(AppResources.Format("Showcase_PortfolioCount", 18, ShowcaseText.PortfolioCapacity), vm.CountLabel);
        Assert.Equal(2, vm.RemainingSlots);
        Assert.True(vm.CanAdd);

        _picker.Setup(p => p.PickAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<byte[]>());
        await vm.AddCommand.ExecuteAsync(null);
        _picker.Verify(p => p.PickAsync(2, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AFullPortfolioDoesNotOpenThePicker()
    {
        var vm = Create(ShowcaseText.PortfolioCapacity);
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.AddCommand.ExecuteAsync(null);

        Assert.True(vm.IsFull);
        Assert.Equal(ShowcaseErrorCopy.For(ShowcaseErrorCodes.PortfolioFull), vm.ErrorMessage);
        _picker.Verify(p => p.PickAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EachPickedPhotoUploadsOnItsOwnTileAndOneFailureCostsOnePhoto()
    {
        var vm = Create(0);
        await vm.LoadCommand.ExecuteAsync(null);
        var good = new byte[] { 1 };
        var bad = new byte[] { 2 };
        _picker.Setup(p => p.PickAsync(20, It.IsAny<CancellationToken>())).ReturnsAsync([good, bad]);
        _api.Setup(a => a.UploadImageAsync(good, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ShowcaseFixtures.Ok(new MediaUpload { Hash = "g" }));
        _api.Setup(a => a.UploadImageAsync(bad, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ShowcaseFixtures.Fail<MediaUpload>(ShowcaseErrorCodes.Undecodable));
        _api.Setup(a => a.AddPortfolioItemAsync("g", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ShowcaseFixtures.Ok(new PortfolioItem { Hash = "g" }));

        await vm.AddCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.Tiles.Count);
        Assert.True(vm.Tiles[0].IsReady);
        Assert.Equal("g", vm.Tiles[0].Hash);
        Assert.True(vm.Tiles[1].IsFailed);
        Assert.Equal(ShowcaseErrorCopy.For(ShowcaseErrorCodes.Undecodable), vm.Tiles[1].ErrorText);
        Assert.Same(bad, vm.Tiles[1].PendingJpeg);
        Assert.Equal(1, vm.StoredCount);
    }

    [Fact]
    public async Task RetryUploadsTheKeptBytesWithoutPickingAgain()
    {
        var vm = Create(0);
        await vm.LoadCommand.ExecuteAsync(null);
        var jpeg = new byte[] { 9 };
        _picker.Setup(p => p.PickAsync(20, It.IsAny<CancellationToken>())).ReturnsAsync([jpeg]);
        _api.SetupSequence(a => a.UploadImageAsync(jpeg, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ShowcaseFixtures.Fail<MediaUpload>(ShowcaseErrorCodes.Network, 0))
            .ReturnsAsync(ShowcaseFixtures.Ok(new MediaUpload { Hash = "r" }));
        _api.Setup(a => a.AddPortfolioItemAsync("r", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ShowcaseFixtures.Ok(new PortfolioItem { Hash = "r" }));
        await vm.AddCommand.ExecuteAsync(null);

        await vm.RetryCommand.ExecuteAsync(vm.Tiles[0]);

        Assert.True(vm.Tiles[0].IsReady);
        Assert.Null(vm.Tiles[0].PendingJpeg);
        _picker.Verify(p => p.PickAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DiscardOnlyRemovesTilesThatNeverStored()
    {
        var vm = Create(1);
        await vm.LoadCommand.ExecuteAsync(null);

        vm.DiscardCommand.Execute(vm.Tiles[0]);

        Assert.Single(vm.Tiles);
    }

    [Fact]
    public async Task TheFirstTileCannotMoveEarlierAndTheLastCannotMoveLater()
    {
        var vm = Create(3);
        await vm.LoadCommand.ExecuteAsync(null);

        var first = vm.ActionsFor(vm.Tiles[0]);
        var last = vm.ActionsFor(vm.Tiles[2]);

        Assert.DoesNotContain(PortfolioTileAction.MakeCover, first);
        Assert.DoesNotContain(PortfolioTileAction.MoveEarlier, first);
        Assert.Contains(PortfolioTileAction.MoveLater, first);
        Assert.DoesNotContain(PortfolioTileAction.MoveLater, last);
        Assert.Contains(PortfolioTileAction.MakeCover, last);
        Assert.All(Enum.GetValues<PortfolioTileAction>(), a =>
            Assert.False(string.IsNullOrWhiteSpace(PortfolioEditorViewModel.LabelFor(a))));
    }

    [Fact]
    public async Task MakeCoverSendsThePermutationWithTheTileFirst()
    {
        var vm = Create(3);
        await vm.LoadCommand.ExecuteAsync(null);
        _api.Setup(a => a.ReorderPortfolioAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ShowcaseFixtures.Ok(WithOrder("h3", "h1", "h2")));

        await vm.ApplyAsync(vm.Tiles[2], PortfolioTileAction.MakeCover);

        _api.Verify(a => a.ReorderPortfolioAsync(
            It.Is<IReadOnlyList<string>>(o => o.SequenceEqual(new[] { "h3", "h1", "h2" })), It.IsAny<CancellationToken>()));
        Assert.Equal("h3", vm.Tiles[0].Hash);
        Assert.True(vm.Tiles[0].IsCover);
    }

    [Fact]
    public async Task APortfolioChangedAnswerReloadsAndSaysSo()
    {
        var vm = Create(2);
        await vm.LoadCommand.ExecuteAsync(null);
        _api.Setup(a => a.ReorderPortfolioAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ShowcaseFixtures.Fail<MyShowcase>(ShowcaseErrorCodes.PortfolioChanged, 409));

        await vm.ApplyAsync(vm.Tiles[1], PortfolioTileAction.MoveEarlier);

        Assert.Equal(ShowcaseErrorCopy.For(ShowcaseErrorCodes.PortfolioChanged), vm.ErrorMessage);
        _api.Verify(a => a.GetMineAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task RemoveIsNotConfirmedAndOffersUndoForFiveSeconds()
    {
        var vm = Create(3);
        await vm.LoadCommand.ExecuteAsync(null);
        _api.Setup(a => a.RemovePortfolioItemAsync("h2", It.IsAny<CancellationToken>())).ReturnsAsync(ShowcaseFixtures.Ok(true));
        Func<Task>? undo = null;
        _alerts.Setup(a => a.ShowAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<Task>>(), It.IsAny<TimeSpan>()))
            .Callback<string, string, Func<Task>, TimeSpan>((_, _, action, _) => undo = action)
            .Returns(Task.CompletedTask);

        await vm.ApplyAsync(vm.Tiles[1], PortfolioTileAction.Remove);

        Assert.Equal(["h1", "h3"], vm.Tiles.Select(t => t.Hash));
        _alerts.Verify(a => a.ShowAsync(
            AppResources.GetString("Portfolio_Removed"),
            AppResources.GetString("General_Undo"),
            It.IsAny<Func<Task>>(),
            PortfolioEditorViewModel.UndoWindow));
        Assert.Equal(TimeSpan.FromSeconds(5), PortfolioEditorViewModel.UndoWindow);
        Assert.NotNull(undo);
    }

    [Fact]
    public async Task UndoReAddsTheSameHashAndCaptionAndRestoresTheOrder()
    {
        var vm = Create(3);
        await vm.LoadCommand.ExecuteAsync(null);
        var removed = vm.Tiles[0];
        _api.Setup(a => a.RemovePortfolioItemAsync("h1", It.IsAny<CancellationToken>())).ReturnsAsync(ShowcaseFixtures.Ok(true));
        Func<Task>? undo = null;
        _alerts.Setup(a => a.ShowAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<Task>>(), It.IsAny<TimeSpan>()))
            .Callback<string, string, Func<Task>, TimeSpan>((_, _, action, _) => undo = action)
            .Returns(Task.CompletedTask);
        _api.Setup(a => a.AddPortfolioItemAsync("h1", "first", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ShowcaseFixtures.Ok(new PortfolioItem { Hash = "h1", Caption = "first" }));
        _api.Setup(a => a.ReorderPortfolioAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ShowcaseFixtures.Ok(WithOrder("h1", "h2", "h3")));

        await vm.RemoveAsync(removed);
        await undo!();

        _api.Verify(a => a.AddPortfolioItemAsync("h1", "first", null, It.IsAny<CancellationToken>()), Times.Once);
        _api.Verify(a => a.ReorderPortfolioAsync(
            It.Is<IReadOnlyList<string>>(o => o.SequenceEqual(new[] { "h1", "h2", "h3" })), It.IsAny<CancellationToken>()));
        Assert.Equal(["h1", "h2", "h3"], vm.Tiles.Select(t => t.Hash));
        _api.Verify(a => a.UploadImageAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AFailedRemoveKeepsTheTileAndOffersNoUndo()
    {
        var vm = Create(2);
        await vm.LoadCommand.ExecuteAsync(null);
        _api.Setup(a => a.RemovePortfolioItemAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ShowcaseFixtures.Fail<bool>(ShowcaseErrorCodes.Network, 0));

        await vm.RemoveAsync(vm.Tiles[0]);

        Assert.Equal(2, vm.Tiles.Count);
        Assert.True(vm.HasError);
        _alerts.Verify(a => a.ShowAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<Task>>(), It.IsAny<TimeSpan>()), Times.Never);
    }

    [Fact]
    public async Task ATooLongCaptionIsNamedAndNotSent()
    {
        var vm = Create(1);
        await vm.LoadCommand.ExecuteAsync(null);

        var saved = await vm.SaveCaptionAsync(vm.Tiles[0], new string('c', ShowcaseText.CaptionMaxLength + 1));

        Assert.False(saved);
        Assert.Equal(AppResources.Format("Showcase_CaptionTooLong", ShowcaseText.CaptionMaxLength), vm.ErrorMessage);
        _api.Verify(a => a.UpdatePortfolioItemAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ASavedCaptionUpdatesTheTile()
    {
        var vm = Create(1);
        await vm.LoadCommand.ExecuteAsync(null);
        _api.Setup(a => a.UpdatePortfolioItemAsync("h1", "New", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ShowcaseFixtures.Ok(new PortfolioItem { Hash = "h1", Caption = "New" }));

        var saved = await vm.SaveCaptionAsync(vm.Tiles[0], " New ");

        Assert.True(saved);
        Assert.Equal("New", vm.Tiles[0].Caption);
    }

    [Fact]
    public async Task ReloadingKeepsTilesStillUploading()
    {
        var vm = Create(1);
        await vm.LoadCommand.ExecuteAsync(null);
        vm.Tiles.Add(new PortfolioTile { State = PortfolioTileState.Failed, PendingJpeg = [1] });

        vm.Apply(ShowcaseFixtures.Mine(portfolio: 2));

        Assert.Equal(3, vm.Tiles.Count);
        Assert.True(vm.Tiles[2].IsFailed);
        Assert.Equal(3, vm.Tiles[2].Position);
    }
}
