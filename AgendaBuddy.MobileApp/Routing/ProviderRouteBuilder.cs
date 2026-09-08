using AgendaBuddy.MobileApp.Models;

namespace AgendaBuddy.MobileApp.Routing;

/// <summary>
/// Route-building logic for <see cref="Services.ProviderApiService"/>'s report and deactivation routes
/// (api-contracts.md §2).
/// </summary>
public static class ProviderRouteBuilder
{
    /// <summary>
    /// <c>GET /api/v1/providers</c> — the browse/directory list. Returns a
    /// <c>DataResponse&lt;PagedResponse&lt;ProviderSummary&gt;&gt;</c> envelope (ADR-023), same shape as
    /// <see cref="CustomerRouteBuilder.Customers"/>'s paged read.
    /// </summary>
    /// <remarks>
    /// <c>bookableOnly=true</c> is sent because this is the customer-facing directory: a provider with no
    /// active, profession-classified service cannot be booked, so offering one dead-ends the flow. It is a
    /// query parameter rather than the route's default — the endpoint itself is a general paginated list
    /// whose <c>totalCount</c> and paging other callers rely on.
    /// </remarks>
    public static RouteSpec Providers(int page = 1, int pageSize = 25, bool bookableOnly = true) =>
        new(HttpMethod.Get,
            $"api/v1/providers?page={page}&pageSize={pageSize}&bookableOnly={(bookableOnly ? "true" : "false")}");

    public static RouteSpec GetProvider(string email) => new(HttpMethod.Get, $"api/v1/providers/{email}");

    /// <summary>
    /// <c>{email}</c> must be the caller's own claim (<c>OwnershipGuard.AssertOwner</c>, ProviderModule.cs).
    /// <c>ProviderEntity</c> requires <c>FirstName</c>/<c>LastName</c>/<c>Email</c>.
    /// </summary>
    public static RouteSpec UpdateProvider(string email) => new(HttpMethod.Put, $"api/v1/providers/{email}");

    /// <summary>
    /// <c>POST /api/v1/providers</c> — creates the domain profile that registration alone does not.
    /// </summary>
    /// <remarks>
    /// The body is a FLAT ProviderEntity, not <c>{"providerEntity": …}</c> as <c>AddProviderCommand</c>'s
    /// single property name suggests; the wrapped form is rejected with 400 for missing Email/FirstName/LastName.
    /// </remarks>
    public static RouteSpec CreateProvider() => new(HttpMethod.Post, "api/v1/providers");

    public static object BuildCreateProviderPayload(
        string email, string firstName, string lastName, string? phoneNumber) =>
        new { email, firstName, lastName, phoneNumber, timeZoneId = TimeZoneInfo.Local.Id };

    public static object BuildUpdateProviderPayload(string email, string firstName, string lastName) =>
        new { email, firstName, lastName };

    /// <summary>
    /// <c>{email}</c> must be the caller's own claim — Provider/Program.cs guards role and ownership, not a
    /// selector. See <c>GET /api/v1/providers/{email}/report</c>.
    /// </summary>
    public static RouteSpec Report(string email) =>
        new(HttpMethod.Get, $"api/v1/providers/{email}/report");

    /// <summary>A provider deactivating themselves — no administrative bypass exists.</summary>
    public static RouteSpec Deactivate(string email) =>
        new(HttpMethod.Post, $"api/v1/providers/{email}/deactivate");

    /// <summary>
    /// <c>PUT /api/v1/providers/{email}/work-hours</c> — the provider's own working-day bounds. A dedicated
    /// route, not a field on <see cref="UpdateProvider"/>, because that one replaces the whole document.
    /// <c>{email}</c> must be the caller's own claim.
    /// </summary>
    public static RouteSpec WorkHours(string email) =>
        new(HttpMethod.Put, $"api/v1/providers/{email}/work-hours");

    /// <summary><c>endHour</c> is exclusive: 8–17 means the last session finishes at 17:00.</summary>
    /// <summary>
    /// <c>PUT /api/v1/providers/{email}/work-week</c> — per-weekday hours. A dedicated route for the same reason
    /// the single-pair sibling is one: <c>PUT /{email}</c> replaces the whole document.
    /// </summary>
    /// <remarks>
    /// The two coexist. The single pair remains the server's fallback for any weekday the week does not mention,
    /// so a provider who has never opened the new screen keeps exactly the hours they had.
    /// </remarks>
    public static RouteSpec WorkWeek(string email) =>
        new(HttpMethod.Put, $"api/v1/providers/{email}/work-week");

    /// <summary>
    /// Payload shape Provider's <c>WorkWeekRequest</c> binds: <c>{"days":[{"day":"Monday",…}]}</c>.
    /// </summary>
    /// <remarks>
    /// <paramref name="days"/> carries the weekday by NAME, because the server takes a string and answers 400 on
    /// an unrecognised one — an integer would model-bind to Sunday (the enum's zero) and rewrite the wrong day.
    /// A closed day sends <c>isClosed</c> and keeps its hours, so re-opening it does not mean re-entering them.
    /// </remarks>
    public static object BuildWorkWeekPayload(IEnumerable<WorkDayHoursDto> days) =>
        new
        {
            days = days.Select(day => new
            {
                day = day.Day.ToString(),
                startHour = day.StartHour,
                endHour = day.EndHour,
                isClosed = day.IsClosed
            }).ToList()
        };

    public static object BuildWorkHoursPayload(int startHour, int endHour) =>
        new { startHour, endHour };

    /// <summary>
    /// <c>PUT /api/v1/providers/{email}/avatar</c> — which built-in mark this account is drawn with. A dedicated
    /// route, not a field on <see cref="UpdateProvider"/>: that one replaces the whole document, and doing so
    /// also resets every nested service's id (agenda-buddy-2wf). <c>{email}</c> must be the caller's own claim.
    /// </summary>
    public static RouteSpec Avatar(string email) => new(HttpMethod.Put, $"api/v1/providers/{email}/avatar");

    /// <summary>Payload shape Provider's <c>AvatarRequest</c> binds. An unknown id is answered 400, not stored.</summary>
    public static object BuildAvatarPayload(string avatarId) => new { avatarId };

    /// <summary>
    /// <c>PUT /api/v1/providers/{email}/consent</c> — acceptance of the Terms and the Privacy Policy. Dedicated
    /// for the same reason <see cref="Avatar"/> is.
    /// </summary>
    public static RouteSpec Consent(string email) => new(HttpMethod.Put, $"api/v1/providers/{email}/consent");

    /// <summary>
    /// Payload shape Provider's <c>ConsentRequest</c> binds.
    /// </summary>
    /// <remarks>
    /// Booleans, not dates: the server stamps the time, because a consent record whose timestamp the consenting
    /// party supplies proves nothing. Both are always sent, so <c>false</c> reads as "not accepted" rather than
    /// "unchanged".
    /// </remarks>
    public static object BuildConsentPayload(bool acceptedTerms, bool acceptedPrivacy) =>
        new { acceptedTerms, acceptedPrivacy };

    /// <summary>
    /// <c>DELETE /api/v1/providers/{email}</c> — erases the profile, and with it the provider's services and
    /// embedded appointment list, then scrubs the address from their customers' records. The <b>domain half</b>
    /// of account deletion.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Not the same operation as <see cref="Deactivate"/>, which keeps everything and only hides the provider
    /// from the directory.
    /// </para>
    /// <para>
    /// Call this <b>before</b> <see cref="AuthRouteBuilder.DeleteAccount"/>: the credential that route deletes is
    /// what authorises this one, so the reverse order strands a live profile nothing can reach.
    /// </para>
    /// </remarks>
    public static RouteSpec DeleteProvider(string email) => new(HttpMethod.Delete, $"api/v1/providers/{email}");
}
