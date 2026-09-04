using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Services;
using AgendaBuddy.MobileApp.ViewModels;
using Moq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.ViewModels;

public class AddServiceViewModelTests
{
    private const string Email = "provider@example.com";

    private static IUserSessionService CreateSession()
    {
        var session = new Mock<IUserSessionService>();
        session.SetupGet(s => s.Email).Returns(Email);
        session.Setup(s => s.RefreshAsync()).Returns(Task.CompletedTask);
        return session.Object;
    }

    private static IProfessionApiService CreateProfessionApi(params string[] professions)
    {
        var api = new Mock<IProfessionApiService>();
        api.Setup(a => a.GetProviderProfessionsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(professions.ToList());
        return api.Object;
    }

    private static AddServiceViewModel Build(
        Mock<IServicesApiService> servicesApi,
        params string[] professions) =>
        new(servicesApi.Object, CreateProfessionApi(professions), CreateSession());

    [Fact]
    public async Task AddServiceParsesFeeAndDurationAndClearsTheForm()
    {
        var api = new Mock<IServicesApiService>();
        List<ServiceItem>? sent = null;
        api.Setup(a => a.AddServicesAsync(Email, It.IsAny<List<ServiceItem>>(), It.IsAny<CancellationToken>()))
           .Callback<string, List<ServiceItem>, CancellationToken>((_, items, _) => sent = items)
           .ReturnsAsync(true);

        var vm = Build(api, "Fitness");
        vm.ServiceName = "Consultation";
        vm.Description = "30 min";
        vm.Fee = "50";
        vm.DurationMinutes = "30";
        vm.ProfessionName = "Fitness";

        await vm.AddServiceCommand.ExecuteAsync(null);

        Assert.NotNull(sent);
        Assert.Equal(30, sent![0].DurationMinutes);
        Assert.Equal(50, sent[0].Fee);
        Assert.Equal("Fitness", sent[0].ProfessionName);
        Assert.Empty(vm.ServiceName);
        Assert.Empty(vm.Description);
    }

    [Fact]
    public async Task AddingRaisesAddedSoThePageCanReturnToTheList()
    {
        var api = new Mock<IServicesApiService>();
        api.Setup(a => a.AddServicesAsync(Email, It.IsAny<List<ServiceItem>>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(true);

        var vm = Build(api, "Fitness");
        vm.ServiceName = "Consultation";
        vm.Description = "30 min";
        vm.ProfessionName = "Fitness";

        var raised = 0;
        vm.Added += (_, _) => raised++;

        await vm.AddServiceCommand.ExecuteAsync(null);

        Assert.Equal(1, raised);
    }

    [Fact]
    public async Task AFailedAddDoesNotNavigateAwayAndKeepsTheFormFilled()
    {
        var api = new Mock<IServicesApiService>();
        api.Setup(a => a.AddServicesAsync(Email, It.IsAny<List<ServiceItem>>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(false);

        var vm = Build(api, "Fitness");
        vm.ServiceName = "Consultation";
        vm.Description = "30 min";
        vm.ProfessionName = "Fitness";

        var raised = 0;
        vm.Added += (_, _) => raised++;

        await vm.AddServiceCommand.ExecuteAsync(null);

        Assert.Equal(0, raised);
        Assert.True(vm.HasError);
        Assert.Equal("Consultation", vm.ServiceName);
    }

    [Fact]
    public async Task AnUnreachableServerDoesNotNavigateAwayEither()
    {
        var api = new Mock<IServicesApiService>();
        api.Setup(a => a.AddServicesAsync(Email, It.IsAny<List<ServiceItem>>(), It.IsAny<CancellationToken>()))
           .ThrowsAsync(new HttpRequestException("gateway down"));

        var vm = Build(api, "Fitness");
        vm.ServiceName = "Consultation";
        vm.Description = "30 min";
        vm.ProfessionName = "Fitness";

        var raised = 0;
        vm.Added += (_, _) => raised++;

        await vm.AddServiceCommand.ExecuteAsync(null);

        Assert.Equal(0, raised);
        Assert.True(vm.HasError);
    }

    [Theory]
    [InlineData("", "desc", "Fitness")]
    [InlineData("name", "", "Fitness")]
    [InlineData("name", "desc", null)]
    [InlineData("   ", "desc", "Fitness")]
    public void SubmitStaysDisabledUntilNameDescriptionAndProfessionAreAllPresent(
        string name, string description, string? profession)
    {
        var vm = Build(new Mock<IServicesApiService>(), "Fitness");
        vm.ServiceName = name;
        vm.Description = description;
        vm.ProfessionName = profession;

        Assert.False(vm.AddServiceCommand.CanExecute(null));
    }

    [Fact]
    public void SubmitIsEnabledOnceTheRequiredFieldsAreFilled()
    {
        var vm = Build(new Mock<IServicesApiService>(), "Fitness");
        vm.ServiceName = "Consultation";
        vm.Description = "30 min";
        vm.ProfessionName = "Fitness";

        Assert.True(vm.AddServiceCommand.CanExecute(null));
    }

    [Fact]
    public async Task LoadingPreselectsTheFirstProfessionSoTheFormIsUsableImmediately()
    {
        var vm = Build(new Mock<IServicesApiService>(), "Fitness", "Tutoring");

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(["Fitness", "Tutoring"], vm.AvailableProfessions);
        Assert.Equal("Fitness", vm.ProfessionName);
        Assert.False(vm.HasNoProfessions);
    }

    [Fact]
    public async Task WithNoProfessionsTheFormIsReplacedByThePrompt()
    {
        var vm = Build(new Mock<IServicesApiService>());

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.True(vm.HasNoProfessions);
        Assert.False(vm.AddServiceCommand.CanExecute(null));
    }

    [Fact]
    public async Task AFailedProfessionLoadSurfacesAnErrorRatherThanAnEmptyPicker()
    {
        var professionApi = new Mock<IProfessionApiService>();
        professionApi.Setup(a => a.GetProviderProfessionsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                     .ThrowsAsync(new HttpRequestException("gateway down"));

        var vm = new AddServiceViewModel(new Mock<IServicesApiService>().Object, professionApi.Object, CreateSession());

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
    }

    [Fact]
    public async Task ANonNumericFeeOrDurationIsSentAsNullRatherThanFailing()
    {
        var api = new Mock<IServicesApiService>();
        List<ServiceItem>? sent = null;
        api.Setup(a => a.AddServicesAsync(Email, It.IsAny<List<ServiceItem>>(), It.IsAny<CancellationToken>()))
           .Callback<string, List<ServiceItem>, CancellationToken>((_, items, _) => sent = items)
           .ReturnsAsync(true);

        var vm = Build(api, "Fitness");
        vm.ServiceName = "Consultation";
        vm.Description = "30 min";
        vm.ProfessionName = "Fitness";
        vm.Fee = "free";
        vm.DurationMinutes = "half an hour";

        await vm.AddServiceCommand.ExecuteAsync(null);

        Assert.NotNull(sent);
        Assert.Null(sent![0].Fee);
        Assert.Null(sent[0].DurationMinutes);
    }
}
