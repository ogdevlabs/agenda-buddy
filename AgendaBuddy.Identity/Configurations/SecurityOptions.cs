namespace AgendaBuddy.Identity.Configurations;

/// <summary>
/// Per-account lockout thresholds, bound from <c>Security:Lockout</c>.
/// </summary>
/// <remarks>
/// <para>
/// Unlike the rate limiter and HSTS, lockout has <b>no</b> enable flag. It needs none: with the default
/// threshold an account locks only after 10 consecutive wrong passwords, and it unlocks itself 15
/// minutes later, so there is nothing a local run needs switched off. Adding a flag would have created a
/// third way for a security control to be silently absent in exchange for nothing.
/// </para>
/// <para>
/// The defaults come from a measurement, not a convention. BCrypt verify at work factor 12 costs
/// <b>262 ms</b> on this hardware (<c>ARCHITECTURE.md</c> §2), i.e. ≈3.8 attempts/sec/core — so
/// password <i>guessing</i> was never the pressing threat and the threshold does not need to be tight.
/// It needs to be loose enough that a provider who mistypes their own password a few times is not
/// locked out of their own business, because password reset does not exist yet.
/// </para>
/// </remarks>
public sealed class LockoutOptions
{
    /// <summary>The configuration section these bind from.</summary>
    public const string Section = "Security:Lockout";

    /// <summary>Consecutive failed logins before the account locks.</summary>
    public int MaxFailedAttempts { get; set; } = 10;

    /// <summary>How long a lock lasts before it expires on its own.</summary>
    public int WindowMinutes { get; set; } = 15;
}

/// <summary>
/// Per-IP rate limiting for the two routes that spend BCrypt, bound from <c>Security:RateLimiting</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Off by default, deliberately, and gated on configuration rather than on
/// <c>IsProduction()</c>.</b> Services run as <b>Production</b> under the local AppHost — verified:
/// <c>/swagger/v1/swagger.json</c> 404s on all seven, because <c>AppHostWiring.cs</c> adds each project
/// with <c>launchProfileName: null</c> while <c>launchSettings.json</c> sets
/// <c>DOTNET_ENVIRONMENT=Development</c> for the AppHost process only. An environment-gated limiter
/// would therefore throttle every local run (design decision D-6).
/// </para>
/// <para>
/// The cost of that choice: a deployment that never sets the flag ships without the
/// control while every document records the feature as delivered — the same shape as a past
/// defect where <c>AssertRole</c> was present in the codebase and never called. Mitigated by warning
/// loudly at startup outside a local run, and by the integration harness switching the flag on so the
/// 429 is asserted against a running service rather than inferred from a policy object.
/// </para>
/// </remarks>
public sealed class RateLimitingOptions
{
    /// <summary>The configuration section these bind from.</summary>
    public const string Section = "Security:RateLimiting";

    /// <summary>The endpoint policy name, applied to <c>login</c> and <c>register</c> only.</summary>
    public const string PolicyName = "auth";

    /// <summary>Registers the limiter on <c>login</c> and <c>register</c>.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Per-IP allowance per minute. 10 is ≈2.6 s of BCrypt CPU per minute per address, against a
    /// legitimate need of two or three attempts.
    /// </summary>
    public int PermitPerMinute { get; set; } = 10;
}
