using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace AgendaBuddy.Provider.Api.Showcase;

/// <summary>
/// Two policies. <c>showcase-go</c> is per IP on the only anonymous route, and gated on
/// <c>Security:RateLimiting:Enabled</c> like every other IP limiter (ADR-033). <c>showcase-by-code</c> is per
/// signed-in user and always on: it is what stops a signed-in account walking the code space.
/// </summary>
public static class ShowcaseRateLimiting
{
    public const string GoPolicy = "showcase-go";
    public const string ByCodePolicy = "showcase-by-code";
    public const int PermitPerMinute = 30;

    public static IServiceCollection AddShowcaseRateLimiter(this IServiceCollection services, IConfiguration configuration)
    {
        var ipLimiting = configuration.GetValue("Security:RateLimiting:Enabled", false);
        var window = TimeSpan.FromMinutes(1);

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.OnRejected = (context, _) =>
            {
                var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var value)
                    ? (int)Math.Ceiling(value.TotalSeconds)
                    : (int)window.TotalSeconds;
                context.HttpContext.Response.Headers.RetryAfter = retryAfter.ToString(CultureInfo.InvariantCulture);
                return ValueTask.CompletedTask;
            };

            limiter.AddPolicy(GoPolicy, httpContext => ipLimiting
                ? RateLimitPartition.GetSlidingWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unattributed",
                    _ => Options(window))
                : RateLimitPartition.GetNoLimiter("off"));

            limiter.AddPolicy(ByCodePolicy, httpContext => RateLimitPartition.GetSlidingWindowLimiter(
                httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous",
                _ => Options(window)));
        });

        return services;
    }

    private static SlidingWindowRateLimiterOptions Options(TimeSpan window) => new()
    {
        PermitLimit = PermitPerMinute,
        Window = window,
        SegmentsPerWindow = 6,
        QueueLimit = 0
    };
}
