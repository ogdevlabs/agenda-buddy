using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AgendaBuddy.IntegrationTests.Harness;
using Library.Entities;

namespace AgendaBuddy.IntegrationTests.Contract;

/// <summary>
/// F-019-T09 / threat T-102 (mitigate now). Real fault injection, not a mocked exception (episode 001's
/// "reasoned, not observed" discipline): forces a genuine unhandled exception on Booking's request path
/// and inspects the actual wire response body.
/// </summary>
[Collection(HarnessCollection.Name)]
public class BookingErrorLeakageTest(ServiceHostFixture<BookingAnchor> host, CryptoSessionFixture crypto)
    : IClassFixture<ServiceHostFixture<BookingAnchor>>
{
    private readonly TokenFactory _tokens = new(crypto);

    [Fact]
    public async Task AnUnhandledExceptionOnTheBookRoute_LeaksNoExceptionDetailInTheWireResponse()
    {
        // agenda-buddy-cy2: a null EmailProvider passes Validot (EmailAddressAttribute.IsValid(null) ==
        // true, no [Required] on AppointmentEntity) and OwnershipGuard (the token's sub matches
        // EmailCustomer), then throws downstream in the provider-lookup chain -- a real, unforced
        // internal exception, not a deliberately-thrown test double.
        using var service = host.StartService("Production");

        var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/booking/appointments")
        {
            Content = JsonContent.Create(new
            {
                EmailProvider = (string?)null,
                EmailCustomer = "leakage-customer@example.com",
                Start = DateTime.UtcNow.AddHours(1),
                End = DateTime.UtcNow.AddHours(2),
                DayOff = false
            }),
            Headers =
            {
                Authorization = new AuthenticationHeaderValue(
                    "Bearer", _tokens.CreateToken("leakage-customer@example.com", TokenFactory.CustomerRole))
            }
        };

        var response = await service.Client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        // Not asserting a specific status code: AgendaBuddyExceptionHandler (F-016) only maps
        // ForbiddenException centrally, so this exception type propagates past it, and Production has
        // no Development-only fallback handler registered -- api-contracts.md §3.3 already documents
        // this class of exception as "surfaces as 500" by design (ADR-022). What matters for T-102 is
        // the BODY, not the code: whichever shape the wire response takes, it must carry none of the
        // exception's own detail.
        Assert.DoesNotContain("Exception", body, StringComparison.Ordinal);
        Assert.DoesNotContain("StackTrace", body, StringComparison.Ordinal);
        Assert.DoesNotContain(" at ", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Booking.Core", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Booking.Api", body, StringComparison.Ordinal);
        Assert.DoesNotContain("System.", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// Party Review (Echo): the sibling above only exercises the unhandled-exception path (500,
    /// AgendaBuddyExceptionHandler). It never proves the OTHER half of T-102 -- a genuine, handled
    /// <c>Result.Fail</c> from inside a command handler actually reaches the wire as
    /// <c>DataResponse&lt;T&gt;.Fail</c>, not swallowed or reshaped. This forces
    /// <c>BookAppointmentCommandHandler.Handle</c>'s real failure branch (a well-formed, valid-looking
    /// provider email that matches no provider document) rather than mocking <c>Result.Fail</c>.
    /// </summary>
    [Fact]
    public async Task BookingANonExistentProvider_ReturnsBadRequest_WithTheHandlersFailureMessageInErrors()
    {
        using var service = host.StartService("Production");

        var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/booking/appointments")
        {
            Content = JsonContent.Create(new
            {
                EmailProvider = "no-such-provider@example.com",
                EmailCustomer = "leakage-customer-2@example.com",
                Start = DateTime.UtcNow.AddHours(1),
                End = DateTime.UtcNow.AddHours(2),
                DayOff = false
            }),
            Headers =
            {
                Authorization = new AuthenticationHeaderValue(
                    "Bearer", _tokens.CreateToken("leakage-customer-2@example.com", TokenFactory.CustomerRole))
            }
        };

        var response = await service.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var wrapper = await response.Content.ReadFromJsonAsync<DataResponse<AppointmentEntity>>(HarnessJson.Options);
        Assert.False(wrapper!.Success);
        Assert.Contains(wrapper.Errors, e => e.Contains("No provider found for no-such-provider@example.com"));
    }
}
