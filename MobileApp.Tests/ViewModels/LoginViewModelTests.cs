using MobileApp.ViewModels;
using Xunit;

namespace MobileApp.Tests.ViewModels;

public class LoginViewModelTests
{
    [Fact]
    public void LoginViewModel_CanBeInstantiated()
    {
        var vm = new LoginViewModel();
        Assert.NotNull(vm);
    }
}
