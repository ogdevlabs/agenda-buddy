using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace AgendaBuddy.IntegrationTests.Harness;

/// <summary>
/// F-016 AC-14: the routes that still catch <c>ForbiddenException</c> locally return <b>exactly one</b>
/// 403, with their body unchanged.
/// </summary>
/// <remarks>
/// <para>
/// <c>PUT /api/v1/providers/{email}</c> keeps its local <c>try/catch</c>
/// (<c>Provider/Program.cs:203</c>) and so must be untouched by T08.
/// </para>
/// <para>
/// <b>What "no double-handling" is actually observable as.</b> From outside HTTP you cannot see
/// <em>which</em> mechanism produced a 403 — both produce the same bytes (see
/// <see cref="ForbiddenContract"/>). What you can see is whether the response was written
/// <em>once</em>: a second write over an already-started response either throws or leaves two
/// concatenated JSON documents, and <c>JsonDocument.Parse</c> rejects trailing content. So a single
/// well-formed ProblemDetails body with the expected property set is the assertion, and the local catch
/// returning before the exception can propagate is a code-structure fact rather than an HTTP-observable
/// one. Stated plainly instead of overclaiming.
/// </para>
/// <para>
/// This test's first version asserted an <em>empty</em> body on the assumption that
/// <c>TypedResults.Forbid()</c> writes none. It failed, correctly: <c>app.UseStatusCodePages()</c> already
/// converts a bodyless 403 into ProblemDetails. See <see cref="ForbiddenContract"/>.
/// </para>
/// <para>
/// ⚠️ <b>Count correction.</b> PRD AC-14 and <c>api-contracts.md</c> §3.1 both say <b>eight</b>
/// hand-written call sites. There are <b>seven</b>: <c>Booking:125,:149,:174</c>, <c>Customer:154</c>,
/// <c>Provider:203</c>, <c>Services:143,:167</c> — verified by grep across every production project. T08
/// removed exactly one of them (Customer's, for AC-13), leaving <b>six</b>. The "8" most likely came from
/// a grep that also matched a comment. Recorded as finding N-2 in the wave-6 standup MOM.
/// </para>
/// </remarks>
[Collection(HarnessCollection.Name)]
public class LocalCatchUnaffectedTest : IClassFixture<ServiceHostFixture<ProviderAnchor>>
{
    private const string Owner = "owner@example.com";
    private const string Stranger = "stranger@example.com";

    private readonly ServiceHostFixture<ProviderAnchor> _host;
    private readonly TokenFactory _tokens;

    public LocalCatchUnaffectedTest(ServiceHostFixture<ProviderAnchor> host, CryptoSessionFixture crypto)
    {
        _host = host;
        _tokens = new TokenFactory(crypto);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task AC14_ARouteKeepingItsLocalCatch_StillReturnsExactlyOne403(string environment)
    {
        using var service = _host.StartService(environment);

        var request = new HttpRequestMessage(HttpMethod.Put, $"api/v1/providers/{Owner}")
        {
            // Valid per ProviderEntity's annotations, so MiniValidator passes and the ownership guard
            // is actually reached.
            Content = JsonContent.Create(new
            {
                FirstName = "Grace",
                LastName = "Hopper",
                Email = Owner,
                Profession = "coach",
            }),
            Headers =
            {
                Authorization = new AuthenticationHeaderValue(
                    "Bearer", _tokens.CreateToken(Stranger, TokenFactory.ProviderRole)),
            },
        };

        var response = await service.Client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // Exactly one 403: a second write over an already-started response would either throw or leave
        // two concatenated documents, and JsonDocument.Parse rejects trailing content by default.
        using var problem = JsonDocument.Parse(body);

        Assert.Equal(
            ForbiddenContract.Properties,
            problem.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));

        Assert.Equal(403, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Forbidden", problem.RootElement.GetProperty("title").GetString());

        // The T-004 guarantee holds on this path too, and here it is inherited rather than implemented.
        Assert.DoesNotContain("ForbiddenException", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("You do not have permission", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("   at ", body, StringComparison.Ordinal);
    }
}

/// <summary>
/// The 403 body shape, shared by both paths that can produce one.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Corrects <c>api-contracts.md</c> §3.1.</b> The design assumed the seven hand-written
/// <c>TypedResults.Forbid()</c> sites returned a <em>bodyless</em> 403 while the new central handler
/// returned ProblemDetails, and that AC-14's "no changed body" therefore meant tolerating two different
/// 403 contracts. Measured: <c>app.UseStatusCodePages()</c> — already registered in every domain service —
/// turns a bodyless 403 into ProblemDetails, so both paths <b>already</b> return the same shape. The
/// outcome is better than the design predicted: one uniform 403 contract, no divergence to document, and
/// nothing for F-015 to special-case.
/// </para>
/// <para>
/// <c>requestId</c> comes from each service's <c>CustomizeProblemDetails</c> extension
/// (<c>Activity.Current?.Id</c>); <c>traceId</c> is added by the ProblemDetails defaults. Both are the same
/// value. Neither is exported to any sink (<c>10-error-handling.md:138</c>), so neither is lookupable yet —
/// unchanged by F-016, and noted so nobody treats it as a support tool.
/// </para>
/// </remarks>
internal static class ForbiddenContract
{
    public static readonly string[] Properties =
        ["requestId", "status", "title", "traceId", "type"];
}
