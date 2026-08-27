using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace AgendaBuddy.IntegrationTests.Harness;

/// <summary>
/// An <b>expired</b> token gets 401 and a valid token for a <b>different subject</b> gets
/// 403 — both against a real route, over HTTP.
/// </summary>
/// <remarks>
/// <para>
/// This is the task that demonstrates the harness can observe what nothing else in this solution can.
/// `11-testing.md:148`: <i>"Every endpoint's auth attribute, validation call, ownership guard, and
/// status-code mapping is unverified end-to-end."</i> Until now that was a statement about the
/// solution's capability, not a to-do.
/// </para>
/// <para>
/// <b>Target route: <c>PUT /api/v1/customers/{email}</c></b> (<c>Customer/Program.cs:144-164</c>). It is
/// chosen because it already has all three pieces — <c>RequireAuthorization()</c>,
/// <c>OwnershipGuard.AssertOwner(user, email)</c>, and a <c>catch (ForbiddenException)</c> that returns
/// <c>Forbid()</c>. T07 lands <b>before</b> T08's central 403 and before T12/T13's authorization fixes,
/// so it has to assert against behaviour that already exists rather than behaviour this feature adds.
/// </para>
/// <para>
/// ⚠️ <b>The body must be valid.</b> <c>MiniValidator.TryValidate</c> runs at <c>:150</c>, <em>before</em>
/// <c>AssertOwner</c> at <c>:153</c>. A request with an empty or invalid body returns 400 and never
/// reaches the ownership check — a test written without a valid body would read as "the guard does not
/// fire". Recorded because it is a trap, and separately because validation preceding authorization is a
/// mild information-disclosure smell: an unauthorized caller can probe validation rules.
/// </para>
/// <para>
/// The owner case is asserted too. Without it, a guard that rejected <em>everything</em> would satisfy
/// the 403 assertion and look correct.
/// </para>
/// </remarks>
[Collection(HarnessCollection.Name)]
public class AuthFailurePathTest : IClassFixture<ServiceHostFixture<CustomerAnchor>>
{
    private const string Owner = "owner@example.com";
    private const string Stranger = "stranger@example.com";

    private readonly ServiceHostFixture<CustomerAnchor> _host;
    private readonly TokenFactory _tokens;

    public AuthFailurePathTest(ServiceHostFixture<CustomerAnchor> host, CryptoSessionFixture crypto)
    {
        _host = host;
        _tokens = new TokenFactory(crypto);
    }

    private static HttpRequestMessage UpdateCustomer(string email, string? bearerToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"api/v1/customers/{email}")
        {
            // Valid per CustomerEntity's [Required]/[EmailAddress] annotations, so MiniValidator passes
            // and execution actually reaches the ownership guard.
            Content = JsonContent.Create(new
            {
                FirstName = "Ada",
                LastName = "Lovelace",
                Email = email,
            }),
        };

        if (bearerToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        return request;
    }

    [Fact]
    public async Task AnUnauthenticatedRequest_Returns401()
    {
        using var service = _host.StartService();

        var response = await service.Client.SendAsync(UpdateCustomer(Owner, bearerToken: null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AnExpiredToken_Returns401()
    {
        // ClockSkew is TimeSpan.Zero (AuthenticationExtensions.cs:42), so a token a minute old is
        // already rejected — no grace window to wait out.
        using var service = _host.StartService();

        var response = await service.Client.SendAsync(
            UpdateCustomer(Owner, _tokens.CreateExpiredToken(Owner)));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AValidTokenForADifferentSubject_Returns403()
    {
        // Authenticated, correctly signed, unexpired — and still refused, because the subject is not
        // the customer being modified. 401 here would mean the token was rejected outright and the
        // ownership check was never exercised, so the distinction between the two codes is the assertion.
        using var service = _host.StartService();

        var response = await service.Client.SendAsync(
            UpdateCustomer(Owner, _tokens.CreateToken(Stranger, TokenFactory.CustomerRole)));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TheOwnersOwnToken_IsNeither401Nor403()
    {
        // The control. A guard that refused everybody would pass the test above.
        using var service = _host.StartService();

        var response = await service.Client.SendAsync(
            UpdateCustomer(Owner, _tokens.CreateToken(Owner, TokenFactory.CustomerRole)));

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
