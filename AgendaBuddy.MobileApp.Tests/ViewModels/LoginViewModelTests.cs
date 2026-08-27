using AgendaBuddy.MobileApp.Services;
using AgendaBuddy.MobileApp.ViewModels;
using Moq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.ViewModels;

public class LoginViewModelTests
{
    private static LoginViewModel BuildSut(Mock<IAuthService> mockAuth)
    {
        var vm = new LoginViewModel(mockAuth.Object);
        vm.Email = "test@example.com";
        vm.Password = "password123";
        return vm;
    }

    // -------------------------------------------------------------------------
    // Test 1: valid credentials → LoginSucceeded raised, ErrorMessage stays empty
    // -------------------------------------------------------------------------
    [Fact]
    public async Task LoginAsync_ValidCredentials_RaisesLoginSucceededAndClearsError()
    {
        var mockAuth = new Mock<IAuthService>();
        mockAuth.Setup(s => s.LoginAsync("test@example.com", "password123", default))
                .ReturnsAsync(true);

        var vm = BuildSut(mockAuth);

        bool eventRaised = false;
        vm.LoginSucceeded += (_, _) => eventRaised = true;

        await vm.SignInCommand.ExecuteAsync(null);

        Assert.True(eventRaised, "LoginSucceeded event should have been raised.");
        Assert.Equal(string.Empty, vm.ErrorMessage);
    }

    // -------------------------------------------------------------------------
    // Test 2: invalid credentials → event NOT raised, ErrorMessage set
    // -------------------------------------------------------------------------
    [Fact]
    public async Task LoginAsync_InvalidCredentials_SetsErrorMessage()
    {
        var mockAuth = new Mock<IAuthService>();
        mockAuth.Setup(s => s.LoginAsync("test@example.com", "password123", default))
                .ReturnsAsync(false);

        var vm = BuildSut(mockAuth);

        bool eventRaised = false;
        vm.LoginSucceeded += (_, _) => eventRaised = true;

        await vm.SignInCommand.ExecuteAsync(null);

        Assert.False(eventRaised, "LoginSucceeded event should NOT have been raised.");
        Assert.Equal("Invalid email or password. Please try again.", vm.ErrorMessage);
    }

    // -------------------------------------------------------------------------
    // Test 3: network error → connection error message
    // -------------------------------------------------------------------------
    [Fact]
    public async Task LoginAsync_NetworkError_SetsConnectionErrorMessage()
    {
        var mockAuth = new Mock<IAuthService>();
        mockAuth.Setup(s => s.LoginAsync("test@example.com", "password123", default))
                .ThrowsAsync(new HttpRequestException("Network unreachable"));

        var vm = BuildSut(mockAuth);

        await vm.SignInCommand.ExecuteAsync(null);

        Assert.Contains("Could not reach the server", vm.ErrorMessage);
    }

    // -------------------------------------------------------------------------
    // Test 4: CanSignIn returns false when Email is empty
    // -------------------------------------------------------------------------
    [Fact]
    public void CanSignIn_EmptyEmail_ReturnsFalse()
    {
        var mockAuth = new Mock<IAuthService>();
        var vm = new LoginViewModel(mockAuth.Object)
        {
            Email = string.Empty,
            Password = "password123"
        };

        Assert.False(vm.SignInCommand.CanExecute(null));
    }

    // -------------------------------------------------------------------------
    // Test 5: CanSignIn returns false when Password is empty
    // -------------------------------------------------------------------------
    [Fact]
    public void CanSignIn_EmptyPassword_ReturnsFalse()
    {
        var mockAuth = new Mock<IAuthService>();
        var vm = new LoginViewModel(mockAuth.Object)
        {
            Email = "test@example.com",
            Password = string.Empty
        };

        Assert.False(vm.SignInCommand.CanExecute(null));
    }

    // -------------------------------------------------------------------------
    // Test 6: CanSignIn returns true when both Email and Password are populated
    // -------------------------------------------------------------------------
    [Fact]
    public void CanSignIn_BothPopulated_ReturnsTrue()
    {
        var mockAuth = new Mock<IAuthService>();
        var vm = new LoginViewModel(mockAuth.Object)
        {
            Email = "test@example.com",
            Password = "password123"
        };

        Assert.True(vm.SignInCommand.CanExecute(null));
    }
}
