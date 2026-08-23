using System.Globalization;
using System.Threading.RateLimiting;
using Identity.Configurations;
using Microsoft.AspNetCore.RateLimiting;

namespace Identity.Extensions;

/// <summary>
/// The per-IP limiter on the two routes that spend BCrypt.
/// </summary>
/// <remarks>
/// <para>
/// <b>What this actually defends against.</b> Not password guessing: BCrypt verify at work factor 12 was
/// <b>measured</b> at 262 ms on this hardware, so an attacker gets ≈3.8 attempts/sec/core and is no
/// closer to a password than they were yesterday. The threat the measurement exposed is the mirror image
/// — every unauthenticated <c>login</c> or <c>register</c> request <i>buys</i> 262 ms of server CPU, so
/// roughly <b>4 requests/sec pins one core</b> and ~31/sec saturates the machine (threat T-101). Identity
/// issues the tokens all six other services validate, so that is a full auth outage from one host on a
/// domestic connection.
/// </para>
/// <para>
/// <b>Both routes, not just login</b> (design decision D-4): <c>RegisterAsync</c> hashes at the same work
/// factor, so limiting <c>login</c> alone would leave an equal-cost vector wide open. <c>refresh</c> is
/// deliberately <b>not</b> limited — it spends no BCrypt, and throttling it would risk breaking a
/// legitimate client's hourly rotation.
/// </para>
/// <para>
/// <b>Per IP in middleware, per account in <c>IdentityService</c></b> (D-3). ASP.NET resolves a partition
/// key from <c>HttpContext</c> <i>before</i> model binding, and the account identifier is in the JSON
/// body — buffering the body in middleware to partition on it is strictly worse than counting where the
/// account is already loaded. The two halves also cover disjoint attacks: Identity verifies an unknown
/// email against a dummy hash to keep enumeration constant-time (threat T-005), so an attacker using
/// random addresses generates <b>no per-account state at all</b> and only the limiter sees them.
/// </para>
/// </remarks>
public static class RateLimitingExtensions
{
    /// <summary>
    /// Partition key for a request when no client address is available.
    /// </summary>
    /// <remarks>
    /// One shared bucket, deliberately. Nothing can be attributed, so the choice is between limiting
    /// everyone together and limiting nobody, and a CPU-exhaustion control that fails open is not a
    /// control. It shows up in the integration harness, where <c>TestServer</c> leaves
    /// <c>RemoteIpAddress</c> null — which is also the shape of a real deployment behind a proxy that
    /// does not forward the address, and the reason F-017 will need <c>UseForwardedHeaders</c>.
    /// </remarks>
    public const string UnattributedPartition = "unattributed";

    /// <summary>
    /// Registers the limiter and the <c>auth</c> policy. Call only when the flag is on: with the flag
    /// off neither this nor <c>UseRateLimiter</c> runs, so the pipeline is byte-for-byte what it was
    /// before F-021 (the "trivially revertible in an incident" requirement).
    /// </summary>
    public static IServiceCollection AddAuthRateLimiter(
        this IServiceCollection services, RateLimitingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var window = TimeSpan.FromMinutes(1);

        services.AddRateLimiter(limiter =>
        {
            // Without this the framework answers 503, which says "this service is broken" rather than
            // "you are going too fast" and gives a client nothing to back off from.
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            limiter.OnRejected = (context, _) =>
            {
                var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var value)
                    ? (int)Math.Ceiling(value.TotalSeconds)
                    : (int)window.TotalSeconds;

                context.HttpContext.Response.Headers.RetryAfter =
                    retryAfter.ToString(CultureInfo.InvariantCulture);

                return ValueTask.CompletedTask;
            };

            limiter.AddPolicy(
                RateLimitingOptions.PolicyName,
                httpContext => RateLimitPartition.GetSlidingWindowLimiter(
                    PartitionKeyFor(httpContext),
                    _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = PermitLimitFor(options),
                        Window = window,
                        SegmentsPerWindow = 6,

                        // No queue. Queueing an expensive request is not throttling it — the CPU still
                        // gets spent, just later, and the caller holds a connection while it waits.
                        QueueLimit = 0
                    }));
        });

        return services;
    }

    /// <summary>The address this request is attributed to, for limiting purposes.</summary>
    public static string PartitionKeyFor(HttpContext httpContext) =>
        httpContext.Connection.RemoteIpAddress?.ToString() ?? UnattributedPartition;

    /// <summary>
    /// The effective per-window allowance, never below 1.
    /// </summary>
    /// <remarks>
    /// A configured <c>0</c> would make <c>SlidingWindowRateLimiterOptions</c> throw at the first
    /// request — turning a typo in a value that exists to be changed without a deploy into a total
    /// outage of <c>login</c>. Clamping keeps the failure mode "tighter than intended" rather than
    /// "authentication is down".
    /// </remarks>
    public static int PermitLimitFor(RateLimitingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return Math.Max(1, options.PermitPerMinute);
    }
}
