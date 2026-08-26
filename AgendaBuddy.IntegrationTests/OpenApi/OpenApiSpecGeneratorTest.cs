using System.Text;
using System.Text.Json;
using AgendaBuddy.IntegrationTests.Harness;

namespace AgendaBuddy.IntegrationTests.OpenApi;

/// <summary>
/// F-018-T16, AC-17/AC-18. The <see cref="OpenApiSpecGenerator"/> mechanism: resolve
/// <c>ISwaggerProvider</c> from a booted host's own DI container and serialize its "v1" document with
/// pinned writer settings — no HTTP request, no Development override (spike-proven; see
/// <c>Booking/Program.cs</c>'s unconditional <c>AddSwaggerGen()</c>).
/// </summary>
/// <remarks>
/// Joins <see cref="HarnessCollection"/> because <c>JWT_PUBLIC_KEY</c> is a single process-wide
/// environment variable (<c>Library.Extensions.AuthenticationExtensions</c> reads it eagerly at
/// DI-registration time) — the same reason every <see cref="ServiceHostFixture{TEntryPoint}"/>
/// consumer joins it. This class never uses a MongoDB container: T16 is deliberately decoupled from
/// the harness (see the task body) — <see cref="OpenApiSpecGenerator"/> supplies its own
/// unreachable-but-syntactically-valid connection string instead.
/// </remarks>
[Collection(HarnessCollection.Name)]
public class OpenApiSpecGeneratorTest(CryptoSessionFixture crypto)
{
    // AC-17's "open item": full-document byte determinism was NOT proven at the spike (only stable
    // path SETS were). Two independent boots of the same service must produce byte-identical output,
    // or AC-19's future drift check would fail on its own noise rather than a real contract change.
    [Fact]
    public void GivenTheSameServiceBootedTwice_WhenGeneratingItsV1Spec_ThenTheBytesAreIdentical()
    {
        var first = OpenApiSpecGenerator.Generate<BookingAnchor>(crypto.PublicKeyPem);
        var second = OpenApiSpecGenerator.Generate<BookingAnchor>(crypto.PublicKeyPem);

        Assert.Equal(Encoding.UTF8.GetBytes(first), Encoding.UTF8.GetBytes(second));
    }

    // AC-18: a service that cannot boot must exit non-zero (here: throw) and no caller may reach the
    // point of writing a file. A malformed JWT_PUBLIC_KEY breaks AddAgendaBuddyAuthentication() at
    // DI-registration time (Library.Extensions.AuthenticationExtensions.cs:26,
    // rsa.ImportFromPem(...)) — a real, not simulated, "cannot boot" failure, since it happens inside
    // the service's own Program.cs during host construction.
    [Fact]
    public void GivenAServiceThatCannotBoot_WhenGenerating_ThenItThrowsAndNoArtifactIsEverWritten()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"openapi-ac18-{Guid.NewGuid():N}.json");

        try
        {
            Assert.False(File.Exists(outputPath));

            Assert.ThrowsAny<Exception>(() =>
            {
                var json = OpenApiSpecGenerator.Generate<BookingAnchor>("not a real PEM key");
                File.WriteAllText(outputPath, json); // must never be reached
            });

            Assert.False(File.Exists(outputPath));
        }
        finally
        {
            // HarnessCollection disables parallelization, but JWT_PUBLIC_KEY is still a single
            // process-wide global read by every later test in this collection — restore it.
            Environment.SetEnvironmentVariable("JWT_PUBLIC_KEY", crypto.PublicKeyPem);
            File.Delete(outputPath);
        }
    }

    // AC-17: the mechanism generalises to all 7 services, not just the one used above for
    // determinism/AC-18. A "v1" document with at least one operation is the same conclusiveness bar
    // the spike used for Booking ("1 path, 3 operations").
    [Theory]
    [MemberData(nameof(AllServiceNames))]
    public void GivenEachOfTheSevenServices_WhenGeneratingItsV1Spec_ThenItIsNonEmptyValidJson(string serviceName)
    {
        var json = OpenApiSpecCatalog.Generators[serviceName](crypto.PublicKeyPem);

        using var document = JsonDocument.Parse(json);
        var paths = document.RootElement.GetProperty("paths");

        Assert.True(paths.EnumerateObject().Any(), $"{serviceName} produced a spec with no paths.");
    }

    public static IEnumerable<object[]> AllServiceNames() =>
        OpenApiSpecCatalog.Generators.Keys.Select(name => new object[] { name });
}
