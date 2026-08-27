namespace AgendaBuddy.IntegrationTests.Harness;

/// <summary>
/// A real service emits <c>Strict-Transport-Security</c> over TLS
/// when the flag is on, and never over plain HTTP.
/// </summary>
/// <remarks>
/// <para>
/// The same behaviour is covered by a unit test in <c>AgendaBuddy.ServiceDefaults.Tests</c> against a
/// hand-built pipeline. This is the version that answers AC-15, because the question it settles is not
/// "does <c>UseHsts</c> work" — it is "does a service <b>this project ships</b> call it, in a position
/// where it runs". Those are different claims, and the second one was once wrongly assumed.
/// </para>
/// <para>
/// <b>Profession</b> is the target: its list route is anonymous by design (reference data, not PII), so
/// the assertion needs no token and cannot be confounded by an auth failure.
/// </para>
/// <para>
/// <b>The host is not <c>localhost</c>.</b> ASP.NET's HSTS middleware skips <c>localhost</c>,
/// <c>127.0.0.1</c> and <c>[::1]</c> by default — a good default this project keeps, since a browser
/// honours a cached HSTS directive for the whole <c>max-age</c> and across projects. Clearing the
/// exclusion list to make this test simpler would test a configuration nothing ships.
/// </para>
/// </remarks>
[Collection(HarnessCollection.Name)]
public class TransportSecurityHeaderTest(ServiceHostFixture<ProfessionAnchor> host)
    : IClassFixture<ServiceHostFixture<ProfessionAnchor>>
{
    private const string HstsHeader = "Strict-Transport-Security";
    private const string ExternalHost = "agenda-buddy.example";
    private const string AnonymousRoute = "api/v1/professions";

    private static readonly Dictionary<string, string> HstsOn = new()
    {
        ["Security:Hsts:Enabled"] = "true",
        ["Security:Hsts:MaxAgeDays"] = "30"
    };

    private static async Task<HttpResponseMessage> GetAsync(
        ServiceHost service, string scheme) =>
        await service.Client.GetAsync($"{scheme}://{ExternalHost}/{AnonymousRoute}");

    [Fact]
    public async Task T103_WhenEnabled_ARealServiceSendsStrictTransportSecurityOverTls()
    {
        using var service = host.StartService(settings: HstsOn);

        var response = await GetAsync(service, "https");

        Assert.True(
            response.Headers.Contains(HstsHeader),
            $"A running Profession service with Security:Hsts:Enabled=true sent no {HstsHeader}. Either "
            + "UseAgendaBuddyTransportSecurity is not called in this service's pipeline, or it is placed "
            + "somewhere the response never passes through. Headers received: "
            + string.Join(", ", response.Headers.Select(header => header.Key)));

        Assert.Contains("max-age=2592000", response.Headers.GetValues(HstsHeader).Single());

        // Conservative by design (ARCHITECTURE.md §8): both of these are the hard-to-reverse parts, and a
        // wrong preload submission outlives the mistake by months. A deployment opts in deliberately.
        var directive = response.Headers.GetValues(HstsHeader).Single();
        Assert.DoesNotContain("includeSubDomains", directive, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("preload", directive, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WhenEnabled_TheHeaderIsAbsentOverPlainHttp()
    {
        // Advice a man in the middle can strip is worse than no advice, because it looks like protection.
        using var service = host.StartService(settings: HstsOn);

        var response = await GetAsync(service, "http");

        Assert.DoesNotContain(HstsHeader, response.Headers.Select(header => header.Key));
    }

    [Fact]
    public async Task AC14_WithTheDefaultConfiguration_NoHstsHeaderIsSentAtAll()
    {
        using var service = host.StartService();

        var overTls = await GetAsync(service, "https");
        var overPlaintext = await GetAsync(service, "http");

        Assert.DoesNotContain(HstsHeader, overTls.Headers.Select(header => header.Key));
        Assert.DoesNotContain(HstsHeader, overPlaintext.Headers.Select(header => header.Key));
    }

    [Fact]
    public async Task TheReorderedRedirectDoesNotBreakOrdinaryHttpRequests()
    {
        // PRD requirement 13 moved UseHttpsRedirection ahead of UseAuthentication in all seven services.
        // The redirect is a no-op wherever no HTTPS port is known — which is every local run, CI run and
        // harness run — and this asserts that rather than leaving it as an assumption, because if it were
        // wrong, every one of the other 100-odd integration tests would start chasing 307s.
        using var service = host.StartService();

        var response = await GetAsync(service, "http");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }
}
