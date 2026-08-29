using AgendaBuddy.IntegrationTests.Harness;
using Xunit;

namespace AgendaBuddy.IntegrationTests.OpenApi;

/// <summary>
/// Rewrites the committed OpenAPI baselines from the same generator
/// <see cref="OpenApiSpecDriftTest"/> checks them against. Opt-in: set
/// <c>REGENERATE_OPENAPI_BASELINES=1</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists.</b> <c>scripts/generate-openapi.sh</c> is NOT the source of the committed
/// baselines and never was, despite its own name and what the drift test's failure message used to
/// advise. The script scrapes each service's live <c>/swagger/v1/swagger.json</c> and reformats it with
/// <c>python3 -m json.tool</c>, which indents with FOUR spaces; the baselines are written by
/// <see cref="OpenApiSpecGenerator"/>'s <c>OpenApiJsonWriter</c>, which indents with TWO. Following that
/// advice rewrites all seven files with the wrong bytes and fails the drift check for every service at
/// once — which is exactly what happened on 2026-08-29, and it is invisible locally because the
/// integration suite is a separate command the unit gate never runs.
/// </para>
/// <para>
/// A test rather than a console project because the generator needs the suite's own
/// <c>CryptoSessionFixture</c> (two services refuse to start without a JWT public key) and the
/// <c>*Anchor</c> aliases that only exist here. Gated on an environment variable so a normal run — CI
/// included — never writes to the repo: a check that silently repairs what it is meant to be checking
/// would assert nothing at all.
/// </para>
/// </remarks>
[Collection(HarnessCollection.Name)]
public class OpenApiSpecBaselineWriter(CryptoSessionFixture crypto)
{
    private const string OptInVariable = "REGENERATE_OPENAPI_BASELINES";

    [Fact]
    public void RegenerateCommittedBaselines_OnlyWhenExplicitlyAskedTo()
    {
        if (Environment.GetEnvironmentVariable(OptInVariable) != "1")
        {
            // Not Skip: a skipped test in the normal suite reads as something unfinished, and this is
            // tooling that is *supposed* to sit inert unless invoked.
            return;
        }

        var directory = Path.Combine(RepoRoot(), "docs", "api", "openapi");
        Directory.CreateDirectory(directory);

        foreach (var (serviceName, generate) in OpenApiSpecCatalog.Generators)
        {
            var path = Path.Combine(directory, $"{serviceName}.json");
            File.WriteAllText(path, generate(crypto.PublicKeyPem));
        }
    }

    private static string RepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null && !File.Exists(Path.Combine(current.FullName, "agenda-buddy.sln")))
        {
            current = current.Parent;
        }

        return current?.FullName
               ?? throw new InvalidOperationException(
                   $"Could not locate repo root (agenda-buddy.sln) walking up from {AppContext.BaseDirectory}.");
    }
}
