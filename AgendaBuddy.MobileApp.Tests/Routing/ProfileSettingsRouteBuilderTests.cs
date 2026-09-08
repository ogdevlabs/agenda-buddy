using System.Text.Json;
using AgendaBuddy.MobileApp.Routing;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Routing;

/// <summary>
/// The avatar, consent and account-deletion routes, for both roles.
/// </summary>
/// <remarks>
/// Route/verb/payload correctness only — the routes themselves are exercised end to end by
/// <c>MobileClientRouteResolutionTest</c> in the integration suite.
/// </remarks>
public class ProfileSettingsRouteBuilderTests
{
    private const string Email = "ada@example.com";

    // ── avatar ─────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A dedicated <c>PUT</c>, not a field on the profile <c>PUT</c> — that one replaces the whole document.
    /// </summary>
    [Fact]
    public void CustomerAvatar_BuildsAPutOnItsOwnSubResource()
    {
        var route = CustomerRouteBuilder.Avatar(Email);

        Assert.Equal(HttpMethod.Put, route.Method);
        Assert.Equal($"api/v1/customers/{Email}/avatar", route.Path);
    }

    [Fact]
    public void ProviderAvatar_BuildsAPutOnItsOwnSubResource()
    {
        var route = ProviderRouteBuilder.Avatar(Email);

        Assert.Equal(HttpMethod.Put, route.Method);
        Assert.Equal($"api/v1/providers/{Email}/avatar", route.Path);
    }

    /// <summary>
    /// ⚠️ <b>No email in the body.</b> The account is the route's <c>{email}</c>, guarded against the caller's own
    /// claim — a body that could name the account would be a body that could change somebody else's.
    /// </summary>
    [Fact]
    public void TheAvatarPayloadCarriesOnlyTheAvatarId()
    {
        var json = JsonSerializer.SerializeToElement(CustomerRouteBuilder.BuildAvatarPayload("avatar_07"));

        Assert.Equal("avatar_07", json.GetProperty("avatarId").GetString());
        Assert.Equal(1, json.EnumerateObject().Count());
    }

    [Fact]
    public void BothRolesAvatarPayloadsAreTheSameShape()
    {
        Assert.Equal(
            JsonSerializer.Serialize(CustomerRouteBuilder.BuildAvatarPayload("avatar_07")),
            JsonSerializer.Serialize(ProviderRouteBuilder.BuildAvatarPayload("avatar_07")));
    }

    // ── consent ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void CustomerConsent_BuildsAPutOnItsOwnSubResource()
    {
        var route = CustomerRouteBuilder.Consent(Email);

        Assert.Equal(HttpMethod.Put, route.Method);
        Assert.Equal($"api/v1/customers/{Email}/consent", route.Path);
    }

    [Fact]
    public void ProviderConsent_BuildsAPutOnItsOwnSubResource()
    {
        var route = ProviderRouteBuilder.Consent(Email);

        Assert.Equal(HttpMethod.Put, route.Method);
        Assert.Equal($"api/v1/providers/{Email}/consent", route.Path);
    }

    /// <summary>
    /// ⚠️ <b>Booleans, not dates — the server stamps the time.</b> A consent record whose timestamp the consenting
    /// party supplies proves nothing, and a device with a wrong clock would file the acceptance in the wrong year.
    /// </summary>
    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void TheConsentPayloadCarriesTwoBooleansAndNoTimestamp(bool terms, bool privacy)
    {
        var json = JsonSerializer.SerializeToElement(
            CustomerRouteBuilder.BuildConsentPayload(terms, privacy));

        Assert.Equal(terms, json.GetProperty("acceptedTerms").GetBoolean());
        Assert.Equal(privacy, json.GetProperty("acceptedPrivacy").GetBoolean());
        Assert.Equal(2, json.EnumerateObject().Count());
    }

    /// <summary>
    /// Both flags are always sent, including <c>false</c>. A partial-update shape would make an unticked box leave
    /// a previous acceptance standing — the one outcome a consent record must never produce.
    /// </summary>
    [Fact]
    public void DecliningIsSentExplicitlyRatherThanOmitted()
    {
        var json = JsonSerializer.SerializeToElement(
            CustomerRouteBuilder.BuildConsentPayload(acceptedTerms: false, acceptedPrivacy: false));

        Assert.False(json.GetProperty("acceptedTerms").GetBoolean());
        Assert.False(json.GetProperty("acceptedPrivacy").GetBoolean());
    }

    [Fact]
    public void BothRolesConsentPayloadsAreTheSameShape()
    {
        Assert.Equal(
            JsonSerializer.Serialize(CustomerRouteBuilder.BuildConsentPayload(true, true)),
            JsonSerializer.Serialize(ProviderRouteBuilder.BuildConsentPayload(true, true)));
    }

    // ── deletion ───────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void DeleteCustomer_BuildsADeleteOnTheProfileItself()
    {
        var route = CustomerRouteBuilder.DeleteCustomer(Email);

        Assert.Equal(HttpMethod.Delete, route.Method);
        Assert.Equal($"api/v1/customers/{Email}", route.Path);
    }

    /// <summary>
    /// ⚠️ Not the same route as <c>POST /{email}/deactivate</c>, which keeps everything and only hides the
    /// provider from the directory.
    /// </summary>
    [Fact]
    public void DeleteProvider_IsADistinctRouteFromDeactivate()
    {
        var delete = ProviderRouteBuilder.DeleteProvider(Email);
        var deactivate = ProviderRouteBuilder.Deactivate(Email);

        Assert.Equal(HttpMethod.Delete, delete.Method);
        Assert.Equal($"api/v1/providers/{Email}", delete.Path);
        Assert.NotEqual(delete.Path, deactivate.Path);
        Assert.NotEqual(delete.Method, deactivate.Method);
    }

    /// <summary>
    /// ⚠️ <b>No email anywhere in the credential-delete route.</b> The account is the caller's <c>sub</c> claim, so
    /// there is nothing to substitute in order to delete somebody else's.
    /// </summary>
    [Fact]
    public void DeleteAccount_TakesNoEmailAtAll()
    {
        var route = AuthRouteBuilder.DeleteAccount();

        Assert.Equal(HttpMethod.Delete, route.Method);
        Assert.Equal("api/v1/auth/account", route.Path);
        Assert.DoesNotContain('@', route.Path);
        Assert.DoesNotContain('{', route.Path);
    }
}
