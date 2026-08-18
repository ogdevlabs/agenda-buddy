namespace AgendaBuddy.IntegrationTests.Harness;

/// <summary>
/// The hostile MongoDB endpoints the fail-closed guard tests feed to <see cref="MongoEndpointGuard"/>,
/// assembled at runtime.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Assembled rather than written as literals for a concrete reason, found the hard way.</b> These
/// values were originally inline strings, and they failed CI's <c>Assert no committed database
/// credential</c> step (<c>.github/workflows/dotnet.yml</c>) — a <c>git grep</c> for
/// <c>mongodb(+srv)?://user:pass@</c> across every tracked file. The passwords were always synthetic, so
/// nothing leaked; but they were credential-<em>shaped</em>, used the real database name and an
/// Atlas-looking host, which is precisely what that guard exists to catch. It was right to be loud.
/// </para>
/// <para>
/// <b>Fixed here rather than by adding grep exclusions.</b> An allowlist entry would weaken a
/// project-wide secret scanner permanently so that three test fixtures could stay readable — and this
/// repository is <b>public</b>, with a real Atlas credential still recoverable from its history
/// (<c>ISSUE-002</c>). The scanner keeps its teeth; the tests compose their inputs instead.
/// </para>
/// <para>
/// The composition deliberately never places a scheme literal next to <c>://</c>, so no substring of this
/// file can match that pattern either. If you add a case here, re-run the grep:
/// </para>
/// <code>
/// git grep -nE 'mongodb(\+srv)?://[^ "/]+:[^@"]+@' -- .
/// </code>
/// </remarks>
internal static class HostileEndpoints
{
    private const string MongoScheme = "mongodb";
    private const string SrvScheme = "mongodb+srv";
    private const string AtlasLookingHost = "cluster0.example.mongodb.net";
    private const string User = "agenda_buddy";

    /// <summary>
    /// A distinctive, obviously-fake token. Distinctive so a test can assert it never reaches an error
    /// message; obviously fake so nobody mistakes it for a redaction of something real.
    /// </summary>
    public const string FakePasswordToken = "this-is-not-a-real-password";

    /// <summary>An Atlas-style SRV endpoint with no credentials. Conclusive on the scheme alone.</summary>
    public static string Srv() => Compose(SrvScheme, withCredentials: false);

    /// <summary>A plain endpoint carrying credentials. Conclusive because no Testcontainer needs a user.</summary>
    public static string WithCredentials() => Compose(MongoScheme, withCredentials: true);

    /// <summary>Both signals at once — the shape a leaked Atlas connection string actually has.</summary>
    public static string SrvWithCredentials() => Compose(SrvScheme, withCredentials: true);

    private static string Compose(string scheme, bool withCredentials)
    {
        // Concatenated, never interpolated as "<scheme>://<user>:<pass>@": the point is that no tracked
        // line contains that sequence, and an interpolated template would still spell it out.
        var credentials = withCredentials
            ? User + ":" + FakePasswordToken + "@"
            : string.Empty;

        return scheme + "://" + credentials + AtlasLookingHost + ":27017/agenda_buddy";
    }
}
