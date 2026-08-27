using AgendaBuddy.MobileApp.Routing;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Routing;

public class AuthRouteBuilderTests
{
    // Pins AuthService.LoginAsync's current (already-correct) route: POST "api/v1/auth/login".
    // Extracted now so refresh/logout wiring can add methods here using the same
    // testable pattern, even though this route does not need correcting.
    [Fact]
    public void Login_BuildsPost()
    {
        var route = AuthRouteBuilder.Login();

        Assert.Equal(HttpMethod.Post, route.Method);
        Assert.Equal("api/v1/auth/login", route.Path);
    }

    // Pins AuthService.RegisterAsync's current (already-correct) route: POST "api/v1/auth/register".
    [Fact]
    public void Register_BuildsPost()
    {
        var route = AuthRouteBuilder.Register();

        Assert.Equal(HttpMethod.Post, route.Method);
        Assert.Equal("api/v1/auth/register", route.Path);
    }

    // AuthService.LogoutAsync calls this in addition to clearing local storage,
    // matching AgendaBuddy.Identity/Program.cs:196's `POST api/v1/auth/logout`.
    [Fact]
    public void Logout_BuildsPost()
    {
        var route = AuthRouteBuilder.Logout();

        Assert.Equal(HttpMethod.Post, route.Method);
        Assert.Equal("api/v1/auth/logout", route.Path);
    }
}
