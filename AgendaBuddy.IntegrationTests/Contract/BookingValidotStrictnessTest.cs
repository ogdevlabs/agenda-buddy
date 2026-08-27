using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AgendaBuddy.IntegrationTests.Harness;
using AgendaBuddy.Library.Entities;

namespace AgendaBuddy.IntegrationTests.Contract;

/// <summary>
/// F-019-T08 / threat T-101 (mitigate now). The only Booking DTO that actually migrated validation
/// libraries in this feature is <see cref="AppointmentEntity"/> (<c>POST /appointments</c>,
/// <c>MiniValidator</c> → Validot, F-019-T02/T04) — every request that 400d under
/// <c>MiniValidator</c> for it must still 400 under Validot. Drives the exact same malformed/missing
/// cases through the real HTTP pipeline, not just the isolated validator
/// (<c>AgendaBuddy.Booking.Tests/Validation/AppointmentEntitySpecificationTest.cs</c> already pins that; this test
/// proves the pipeline actually wires it the same way).
/// </summary>
/// <remarks>
/// <b>Why not "every one of Booking's 10 routes' request DTOs" literally.</b> The other 9 DTOs did not
/// migrate to Validot in this feature — Update/Cancel (<c>AppointmentEntity</c> via
/// <c>MiniValidator</c>) and all 7 F-014 routes (inline checks, e.g. <c>string.IsNullOrWhiteSpace</c>)
/// are byte-for-byte unchanged (F-019-T04/T06 explicitly preserved their failure branches). "Regression
/// test the migration" for a DTO that never migrated is vacuous — disclosed here, not silently skipped,
/// and left for a future task (F-020 or a dedicated follow-up) if Update/Cancel/F-014 DTOs ever move to
/// Validot too.
/// </remarks>
[Collection(HarnessCollection.Name)]
public class BookingValidotStrictnessTest(ServiceHostFixture<BookingAnchor> host, CryptoSessionFixture crypto)
    : IClassFixture<ServiceHostFixture<BookingAnchor>>
{
    private readonly TokenFactory _tokens = new(crypto);

    private static HttpRequestMessage BookRequest(string token, object payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/booking/appointments")
        {
            Content = JsonContent.Create(payload),
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) }
        };
        return request;
    }

    // Validation runs before the ownership/provider lookup in the route (F-019-T02's spike, verified
    // again here live), so no seeded provider is needed for any of these 400 cases to reach Validot.

    [Fact]
    public async Task MalformedProviderEmail_Returns400_MatchingMiniValidatorToday()
    {
        using var service = host.StartService("Production");

        var response = await service.Client.SendAsync(BookRequest(
            _tokens.CreateToken("someone@example.com", TokenFactory.ProviderRole),
            new
            {
                EmailProvider = "not-an-email",
                EmailCustomer = "customer@example.com",
                Start = DateTime.UtcNow.AddHours(1),
                End = DateTime.UtcNow.AddHours(2),
                DayOff = false
            }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EmptyCustomerEmail_Returns400_MatchingMiniValidatorToday()
    {
        using var service = host.StartService("Production");

        var response = await service.Client.SendAsync(BookRequest(
            _tokens.CreateToken("someone@example.com", TokenFactory.ProviderRole),
            new
            {
                EmailProvider = "provider@example.com",
                EmailCustomer = "",
                Start = DateTime.UtcNow.AddHours(1),
                End = DateTime.UtcNow.AddHours(2),
                DayOff = false
            }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
