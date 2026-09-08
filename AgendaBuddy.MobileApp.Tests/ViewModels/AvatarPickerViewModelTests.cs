using AgendaBuddy.Library.Avatars;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Services;
using AgendaBuddy.MobileApp.ViewModels;
using Moq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.ViewModels;

/// <summary>
/// Choosing an avatar from the built-in set.
/// </summary>
public class AvatarPickerViewModelTests
{
    private const string Email = "ada@example.com";

    private static AvatarPickerViewModel Build(
        out Mock<IProviderApiService> provider,
        out Mock<ICustomerApiService> customer,
        bool isProvider = false,
        string? storedAvatarId = null,
        bool profileReadThrows = false,
        bool saveSucceeds = true)
    {
        provider = new Mock<IProviderApiService>();
        customer = new Mock<ICustomerApiService>();

        var profile = new ProfileInfo { Email = Email, AvatarId = storedAvatarId ?? string.Empty };

        if (profileReadThrows)
        {
            provider.Setup(p => p.GetProfileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new HttpRequestException("offline"));
            customer.Setup(c => c.GetProfileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new HttpRequestException("offline"));
        }
        else
        {
            provider.Setup(p => p.GetProfileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(profile);
            customer.Setup(c => c.GetProfileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(profile);
        }

        provider.Setup(p => p.SetAvatarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(saveSucceeds);
        customer.Setup(c => c.SetAvatarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(saveSucceeds);

        var session = new Mock<IUserSessionService>();
        session.SetupGet(s => s.Email).Returns(Email);
        session.SetupGet(s => s.Role).Returns(isProvider ? "Provider" : "Customer");
        session.SetupGet(s => s.IsProvider).Returns(isProvider);
        session.SetupGet(s => s.IsCustomer).Returns(!isProvider);
        session.Setup(s => s.RefreshAsync()).Returns(Task.CompletedTask);

        return new AvatarPickerViewModel(provider.Object, customer.Object, session.Object);
    }

    /// <summary>
    /// ⚠️ <b>The grid is the shared catalogue, not a list declared in the client.</b> The server assigns from the
    /// same one and refuses an id it does not know, so a second list would drift into offering marks that cannot be
    /// saved — or storing ids the client has no asset for, which renders as an empty circle rather than an error.
    /// </summary>
    [Fact]
    public async Task TheGridOffersExactlyTheSharedCatalogue()
    {
        var vm = Build(out _, out _);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(AvatarCatalog.Count, vm.Choices.Count);
        Assert.Equal(AvatarCatalog.Ids, vm.Choices.Select(choice => choice.Id));
    }

    [Fact]
    public async Task LoadingTwiceDoesNotDuplicateTheGrid()
    {
        var vm = Build(out _, out _);

        await vm.LoadCommand.ExecuteAsync(null);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(AvatarCatalog.Count, vm.Choices.Count);
    }

    [Fact]
    public async Task TheStoredAvatarOpensRinged()
    {
        var stored = AvatarCatalog.Ids[9];
        var vm = Build(out _, out _, storedAvatarId: stored);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(stored, vm.SelectedAvatarId);
        Assert.Single(vm.Choices.Where(choice => choice.IsSelected));
        Assert.True(vm.Choices.Single(choice => choice.Id == stored).IsSelected);
    }

    /// <summary>
    /// An account with no stored avatar is already being drawn with the email-derived mark everywhere else, so that
    /// is the tile to ring — anything else would show the user a selection that contradicts every other screen.
    /// </summary>
    [Fact]
    public async Task AnAccountWithNoStoredAvatarOpensOnTheMarkItIsAlreadyDrawnWith()
    {
        var vm = Build(out _, out _, storedAvatarId: "");

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(AvatarCatalog.Deterministic(Email), vm.SelectedAvatarId);
    }

    /// <summary>
    /// An id from a build with a larger catalogue is treated as absent rather than honoured, matching
    /// <c>AvatarCatalog.Resolve</c> — the alternative is ringing no tile at all while the rest of the app draws one.
    /// </summary>
    [Fact]
    public async Task AnUnknownStoredAvatarFallsBackToTheDerivedMark()
    {
        var vm = Build(out _, out _, storedAvatarId: "avatar_99");

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(AvatarCatalog.Deterministic(Email), vm.SelectedAvatarId);
    }

    /// <summary>
    /// 24 local images are still worth drawing when one request failed. Refusing to open the grid would be the
    /// worse answer, so the read failure is reported and the screen stays usable.
    /// </summary>
    [Fact]
    public async Task AFailedProfileReadStillDrawsTheGrid()
    {
        var vm = Build(out _, out _, profileReadThrows: true);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(AvatarCatalog.Count, vm.Choices.Count);
        Assert.True(vm.HasError);
        Assert.False(vm.IsLoading);
    }

    /// <summary>Selection is selection. Nothing is written until Save, so a mistap is not a write.</summary>
    [Fact]
    public async Task SelectingATileWritesNothing()
    {
        var vm = Build(out _, out var customer);
        await vm.LoadCommand.ExecuteAsync(null);

        vm.SelectCommand.Execute(vm.Choices[3]);

        Assert.Equal(vm.Choices[3].Id, vm.SelectedAvatarId);
        customer.Verify(c => c.SetAvatarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SelectingATileMovesTheRingRatherThanAddingASecondOne()
    {
        var vm = Build(out _, out _, storedAvatarId: AvatarCatalog.Ids[0]);
        await vm.LoadCommand.ExecuteAsync(null);

        vm.SelectCommand.Execute(vm.Choices[5]);

        Assert.Single(vm.Choices.Where(choice => choice.IsSelected));
        Assert.True(vm.Choices[5].IsSelected);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SavingWritesTheChosenIdThroughTheCallersOwnRoleRoute(bool isProvider)
    {
        var vm = Build(out var provider, out var customer, isProvider: isProvider,
            storedAvatarId: AvatarCatalog.Ids[0]);
        await vm.LoadCommand.ExecuteAsync(null);
        vm.SelectCommand.Execute(vm.Choices[5]);

        await vm.SaveCommand.ExecuteAsync(null);

        if (isProvider)
        {
            provider.Verify(p => p.SetAvatarAsync(Email, AvatarCatalog.Ids[5], It.IsAny<CancellationToken>()), Times.Once);
            customer.Verify(c => c.SetAvatarAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }
        else
        {
            customer.Verify(c => c.SetAvatarAsync(Email, AvatarCatalog.Ids[5], It.IsAny<CancellationToken>()), Times.Once);
            provider.Verify(p => p.SetAvatarAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }

    [Fact]
    public async Task SavingRaisesTheEventThatReturnsToTheProfile()
    {
        var vm = Build(out _, out _, storedAvatarId: AvatarCatalog.Ids[0]);
        await vm.LoadCommand.ExecuteAsync(null);
        vm.SelectCommand.Execute(vm.Choices[5]);
        var saved = false;
        vm.Saved += (_, _) => saved = true;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.True(saved);
    }

    [Fact]
    public async Task AFailedSaveIsReportedAndStaysOnThePage()
    {
        var vm = Build(out _, out _, storedAvatarId: AvatarCatalog.Ids[0], saveSucceeds: false);
        await vm.LoadCommand.ExecuteAsync(null);
        vm.SelectCommand.Execute(vm.Choices[5]);
        var saved = false;
        vm.Saved += (_, _) => saved = true;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.False(saved);
        Assert.True(vm.HasError);
        Assert.False(vm.IsSaving);
    }

    /// <summary>Nothing to save when nothing changed.</summary>
    [Fact]
    public async Task SavingIsBlockedWhenTheStoredMarkIsAlreadyTheSelectedOne()
    {
        var stored = AvatarCatalog.Ids[9];
        var vm = Build(out _, out _, storedAvatarId: stored);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.False(vm.SaveCommand.CanExecute(null));
    }

    /// <summary>
    /// ⚠️ An account with no stored avatar <b>can</b> save the mark it was already being drawn with — that turns
    /// the derived fallback into a real, recorded choice. Without this, the tile ringed on open would be
    /// unsavable, which reads as the button being broken.
    /// </summary>
    [Fact]
    public async Task AnAccountWithNoStoredAvatarCanSaveTheDerivedMarkAsItsOwn()
    {
        var vm = Build(out _, out _, storedAvatarId: "");

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.True(vm.SaveCommand.CanExecute(null));
    }
}
