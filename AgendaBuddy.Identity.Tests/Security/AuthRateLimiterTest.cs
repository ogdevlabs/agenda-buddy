using System.Net;
using AgendaBuddy.Identity.Configurations;
using AgendaBuddy.Identity.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace AgendaBuddy.Identity.Tests.Security;

/// <summary>
/// The parts of the per-IP limiter that can be asserted without a running service.
/// </summary>
/// <remarks>
/// <b>These are not the test that matters.</b> A unit test on a policy object passes happily while the
/// middleware is unregistered or the policy is attached to no endpoint — which is exactly the shape of
/// a past defect where <c>AssertRole</c> existed in the codebase and was never called. AC-6
/// is therefore asserted against a <b>running service</b> in
/// <c>AgendaBuddy.IntegrationTests/Harness/AuthRateLimitTest.cs</c>. What is worth pinning here is the
/// rejection contract (429 rather than the framework's default 503) and the partition-key fallback, both
/// cheap and both easy to get wrong in a way integration output would not explain.
/// </remarks>
public class AuthRateLimiterTest
{
    private static ServiceProvider ProviderWith(RateLimitingOptions options)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthRateLimiter(options);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void T101_ARejectedRequestAnswers429_NotTheFrameworkDefault503()
    {
        // 503 says "this service is broken", which is both wrong and useless to a client: there is
        // nothing to back off from and no Retry-After to read.
        var limiter = ProviderWith(new RateLimitingOptions { Enabled = true, PermitPerMinute = 3 })
            .GetRequiredService<IOptions<RateLimiterOptions>>();

        Assert.Equal((int)HttpStatusCode.TooManyRequests, limiter.Value.RejectionStatusCode);
    }

    [Fact]
    public void TheLimiterIsRegisteredAsAService_SoUseRateLimiterCanResolveIt()
    {
        var provider = ProviderWith(new RateLimitingOptions { Enabled = true });

        Assert.NotNull(provider.GetService<IOptions<RateLimiterOptions>>());
    }

    [Fact]
    public void RequestsWithAKnownAddress_ArePartitionedByIt()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("198.51.100.7");

        Assert.Equal("198.51.100.7", RateLimitingExtensions.PartitionKeyFor(context));
    }

    [Fact]
    public void RequestsWithNoAddress_ShareOneBucketRatherThanBypassingTheLimit()
    {
        // TestServer leaves RemoteIpAddress null, and so does a real deployment behind a proxy that does
        // not forward the client address — which needs UseForwardedHeaders to fix. Until
        // then, one shared bucket is the only honest option: a CPU-exhaustion control that fails open
        // when it cannot attribute a request is not a control.
        Assert.Equal(
            RateLimitingExtensions.UnattributedPartition,
            RateLimitingExtensions.PartitionKeyFor(new DefaultHttpContext()));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(1, 1)]
    [InlineData(10, 10)]
    public void APermitLimitOfZeroOrLess_IsClampedToOne(int configured, int effective)
    {
        // A configured 0 makes SlidingWindowRateLimiterOptions throw at the first request, so a typo in a
        // value that exists to be changed without a deploy would take login down completely. The failure
        // mode has to be "tighter than intended", never "authentication is unavailable".
        Assert.Equal(
            effective,
            RateLimitingExtensions.PermitLimitFor(
                new RateLimitingOptions { Enabled = true, PermitPerMinute = configured }));
    }

    [Fact]
    public void TheDefaultAllowance_IsTheMeasuredOne()
    {
        // 10 per minute ≈ 2.6 s of BCrypt CPU per minute per address, against a legitimate need of two
        // or three attempts. The number came from measuring the verify cost, not from convention — if
        // someone changes it, they should have a new measurement.
        Assert.Equal(10, new RateLimitingOptions().PermitPerMinute);
        Assert.False(new RateLimitingOptions().Enabled);
    }
}
