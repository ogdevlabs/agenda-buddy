using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace AgendaBuddy.IntegrationTests.Harness;

/// <summary>
/// F-016 AC-13, AC-14 and AC-23 (`[security]`, threat <b>T-004</b>): the central 403, over real HTTP, in
/// both environments.
/// </summary>
/// <remarks>
/// <para>
/// <c>PUT /api/v1/customers/{email}</c> had its local <c>try/catch (ForbiddenException)</c> removed by
/// T08 (<c>Customer/Program.cs:153</c>), so it now relies entirely on
/// <c>AgendaBuddyExceptionHandler</c>. Before T08 that same line without a catch produced a 500 — and in
/// <c>Production</c>, a bare empty-bodied one.
/// </para>
/// <para>
/// ⚠️ <b>Both environments are asserted deliberately.</b> The Development-only
/// <c>UseExceptionHandler</c> lambda <em>wraps</em> the endpoints, and an exception propagates outward, so
/// whichever handler is innermost wins. If <c>app.UseExceptionHandler()</c> were registered before the
/// <c>IsDevelopment()</c> block, that lambda would swallow <c>ForbiddenException</c> and this would pass
/// in <c>Production</c> and fail in <c>Development</c> — green in CI, red on a developer's machine.
/// A single-environment test would not catch that.
/// </para>
/// </remarks>
[Collection(HarnessCollection.Name)]
public class CentralForbiddenTest : IClassFixture<ServiceHostFixture<CustomerAnchor>>
{
    private const string Owner = "owner@example.com";
    private const string Stranger = "stranger@example.com";

    private readonly ServiceHostFixture<CustomerAnchor> _host;
    private readonly TokenFactory _tokens;

    public CentralForbiddenTest(ServiceHostFixture<CustomerAnchor> host, CryptoSessionFixture crypto)
    {
        _host = host;
        _tokens = new TokenFactory(crypto);
    }

    /// <summary>
    /// A request that reaches <c>OwnershipGuard.AssertOwner</c> and fails it. The body must be valid:
    /// <c>MiniValidator</c> runs before the guard, so an invalid one returns 400 and never gets there.
    /// </summary>
    private HttpRequestMessage ForbiddenUpdate() =>
        new(HttpMethod.Put, $"api/v1/customers/{Owner}")
        {
            Content = JsonContent.Create(new { FirstName = "Ada", LastName = "Lovelace", Email = Owner }),
            Headers =
            {
                Authorization = new AuthenticationHeaderValue(
                    "Bearer", _tokens.CreateToken(Stranger, TokenFactory.CustomerRole)),
            },
        };

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task AC13_ARouteWithNoLocalCatch_Returns403NotAn500_InEveryEnvironment(string environment)
    {
        using var service = _host.StartService(environment);

        var response = await service.Client.SendAsync(ForbiddenUpdate());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task T004_TheProductionForbiddenBody_CarriesOnlyStatusTitleAndRequestId()
    {
        // The egress half of the change, and the reason T-004 exists. Production previously emitted NO
        // body at all for an unhandled ForbiddenException, which was accidentally the most conservative
        // behaviour available. T08 starts emitting one, so what is in it is the entire safety margin.
        using var service = _host.StartService("Production");

        var response = await service.Client.SendAsync(ForbiddenUpdate());
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        using var problem = JsonDocument.Parse(body);

        // The same property set the local-catch path returns (see ForbiddenContract). Asserting it on
        // both sides is what turns "no changed body" from an assumption into a verified claim: one
        // uniform 403 contract regardless of which mechanism produced it.
        Assert.Equal(
            ForbiddenContract.Properties,
            problem.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));

        Assert.Equal(403, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Forbidden", problem.RootElement.GetProperty("title").GetString());
        Assert.True(
            problem.RootElement.TryGetProperty("requestId", out var requestId)
            && !string.IsNullOrWhiteSpace(requestId.GetString()),
            $"requestId is missing from the 403 body. It comes from each service's "
            + $"CustomizeProblemDetails extension, which only runs if the handler writes through "
            + $"IProblemDetailsService. Body was: {body}");

        // No exception type, no exception message, no stack frame — in the response actually sent.
        Assert.DoesNotContain("ForbiddenException", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("You do not have permission", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stackTrace", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("   at ", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddingTheHandler_DidNotTurn401IntoSomethingElse()
    {
        // The pipeline gained a middleware. 401 is produced by the authorization middleware, which sits
        // inside the new handler, so this confirms the insertion did not capture it.
        using var service = _host.StartService("Production");

        var request = new HttpRequestMessage(HttpMethod.Put, $"api/v1/customers/{Owner}")
        {
            Content = JsonContent.Create(new { FirstName = "Ada", LastName = "Lovelace", Email = Owner }),
        };

        var response = await service.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
