using AgendaBuddy.MobileApp.Routing;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Routing;

public class ShowcaseRouteBuilderTests
{
    public static TheoryData<string, string, string> AuthoringRoutes => new()
    {
        { nameof(ShowcaseRouteBuilder.Mine), "GET", "api/v1/showcase/me" },
        { nameof(ShowcaseRouteBuilder.SetText), "PUT", "api/v1/showcase/me/text" },
        { nameof(ShowcaseRouteBuilder.SetPhoto), "PUT", "api/v1/showcase/me/photo" },
        { nameof(ShowcaseRouteBuilder.SetLogo), "PUT", "api/v1/showcase/me/logo" },
        { nameof(ShowcaseRouteBuilder.AddPortfolioItem), "POST", "api/v1/showcase/me/portfolio" },
        { nameof(ShowcaseRouteBuilder.ReorderPortfolio), "PUT", "api/v1/showcase/me/portfolio/order" },
        { nameof(ShowcaseRouteBuilder.PublicCode), "POST", "api/v1/showcase/me/code" },
        { nameof(ShowcaseRouteBuilder.Lookup), "POST", "api/v1/showcase/lookup" },
        { nameof(ShowcaseRouteBuilder.Hidden), "GET", "api/v1/showcase/hidden" },
    };

    [Theory]
    [MemberData(nameof(AuthoringRoutes))]
    public void ParameterlessRoutesMatchTheContract(string name, string verb, string path)
    {
        var route = name switch
        {
            nameof(ShowcaseRouteBuilder.Mine) => ShowcaseRouteBuilder.Mine(),
            nameof(ShowcaseRouteBuilder.SetText) => ShowcaseRouteBuilder.SetText(),
            nameof(ShowcaseRouteBuilder.SetPhoto) => ShowcaseRouteBuilder.SetPhoto(),
            nameof(ShowcaseRouteBuilder.SetLogo) => ShowcaseRouteBuilder.SetLogo(),
            nameof(ShowcaseRouteBuilder.AddPortfolioItem) => ShowcaseRouteBuilder.AddPortfolioItem(),
            nameof(ShowcaseRouteBuilder.ReorderPortfolio) => ShowcaseRouteBuilder.ReorderPortfolio(),
            nameof(ShowcaseRouteBuilder.PublicCode) => ShowcaseRouteBuilder.PublicCode(),
            nameof(ShowcaseRouteBuilder.Lookup) => ShowcaseRouteBuilder.Lookup(),
            nameof(ShowcaseRouteBuilder.Hidden) => ShowcaseRouteBuilder.Hidden(),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };

        Assert.Equal(new HttpMethod(verb), route.Method);
        Assert.Equal(path, route.Path);
    }

    [Fact]
    public void UpdatePortfolioItemIsAPatchOnTheHash()
    {
        var route = ShowcaseRouteBuilder.UpdatePortfolioItem("abc123");

        Assert.Equal(HttpMethod.Patch, route.Method);
        Assert.Equal("api/v1/showcase/me/portfolio/abc123", route.Path);
    }

    [Fact]
    public void RemovePortfolioItemIsADeleteOnTheHash()
    {
        var route = ShowcaseRouteBuilder.RemovePortfolioItem("abc123");

        Assert.Equal(HttpMethod.Delete, route.Method);
        Assert.Equal("api/v1/showcase/me/portfolio/abc123", route.Path);
    }

    [Theory]
    [InlineData(ShowcaseSource.Directory, "directory")]
    [InlineData(ShowcaseSource.Scan, "scan")]
    [InlineData(ShowcaseSource.Code, "code")]
    [InlineData(ShowcaseSource.Appointment, "appointment")]
    [InlineData(ShowcaseSource.Message, "message")]
    [InlineData(ShowcaseSource.Booking, "booking")]
    public void ViewCarriesTheSourceTheServerRecognises(ShowcaseSource source, string expected)
    {
        var route = ShowcaseRouteBuilder.View("66f1a2b3c4d5e6f708192a3b", source);

        Assert.Equal(HttpMethod.Get, route.Method);
        Assert.Equal($"api/v1/showcase/66f1a2b3c4d5e6f708192a3b?source={expected}", route.Path);
    }

    [Fact]
    public void EverySourceHasItsOwnWireValue()
    {
        var values = Enum.GetValues<ShowcaseSource>().Select(ShowcaseRouteBuilder.SourceValue).ToList();

        Assert.Equal(values.Count, values.Distinct().Count());
    }

    [Fact]
    public void ByCodeIsUnderItsOwnSegmentSoACodeCannotBeReadAsAProviderRef()
    {
        var route = ShowcaseRouteBuilder.ByCode("K7Q2X9", ShowcaseSource.Scan);

        Assert.Equal(HttpMethod.Get, route.Method);
        Assert.Equal("api/v1/showcase/by-code/K7Q2X9?source=scan", route.Path);
    }

    [Fact]
    public void ReportHideAndUnhideAreKeyedByProviderRef()
    {
        Assert.Equal(new RouteSpec(HttpMethod.Post, "api/v1/showcase/p1/report"), ShowcaseRouteBuilder.Report("p1"));
        Assert.Equal(new RouteSpec(HttpMethod.Put, "api/v1/showcase/p1/hide"), ShowcaseRouteBuilder.Hide("p1"));
        Assert.Equal(new RouteSpec(HttpMethod.Delete, "api/v1/showcase/p1/hide"), ShowcaseRouteBuilder.Unhide("p1"));
    }

    [Fact]
    public void APathSegmentIsEscaped()
    {
        var route = ShowcaseRouteBuilder.ByCode("a/b", ShowcaseSource.Code);

        Assert.Equal("api/v1/showcase/by-code/a%2Fb?source=code", route.Path);
    }
}

public class MediaRouteBuilderTests
{
    [Fact]
    public void UploadIsAPostToTheCollection()
    {
        var route = MediaRouteBuilder.Upload();

        Assert.Equal(HttpMethod.Post, route.Method);
        Assert.Equal("api/v1/media", route.Path);
    }

    [Theory]
    [InlineData(MediaVariant.Thumb, "thumb")]
    [InlineData(MediaVariant.Full, "full")]
    public void FetchNamesProviderHashAndVariant(MediaVariant variant, string expected)
    {
        var route = MediaRouteBuilder.Fetch("66f1a2b3c4d5e6f708192a3b", "9f2ce1", variant);

        Assert.Equal(HttpMethod.Get, route.Method);
        Assert.Equal($"api/v1/media/66f1a2b3c4d5e6f708192a3b/9f2ce1/{expected}", route.Path);
    }
}
