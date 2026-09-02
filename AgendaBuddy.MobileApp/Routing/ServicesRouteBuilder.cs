using AgendaBuddy.Library.Entities;

namespace AgendaBuddy.MobileApp.Routing;

/// <summary>
/// The Services domain service's catalogue routes. <c>{email}</c> is the owning provider — GET has no
/// ownership guard server-side, but PUT/PATCH do (<c>ServicesModule.cs</c>'s <c>OwnershipGuard.AssertOwner</c>),
/// so only the signed-in provider can ever call PUT/PATCH for their own email.
/// </summary>
public static class ServicesRouteBuilder
{
    public static RouteSpec GetServices(string email) => new(HttpMethod.Get, $"api/v1/services/{email}");

    /// <summary>
    /// Appends new services to the catalogue (<c>AddServicesToProviderCommandHandler</c> — server-generated
    /// ids, never send one). Body is a bare JSON array, not a wrapper object.
    /// </summary>
    public static RouteSpec AddServices(string email) => new(HttpMethod.Put, $"api/v1/services/{email}");

    /// <summary>
    /// Updates existing services, matched by <c>Name</c> server-side
    /// (<c>UpdateServicesFromProviderCommandHandler</c>) — a name with no match is silently skipped.
    /// </summary>
    public static RouteSpec UpdateServices(string email) => new(HttpMethod.Patch, $"api/v1/services/{email}");

    /// <summary>
    /// Removes one service, matched by <c>Name</c> in the path — same key everything else in this route
    /// group uses (<c>RemoveServiceFromProviderCommandHandler</c>). <c>Uri.EscapeDataString</c> because a
    /// service name is free text and commonly contains spaces (e.g. "Personal Training Session").
    /// </summary>
    public static RouteSpec RemoveService(string email, string name) =>
        new(HttpMethod.Delete, $"api/v1/services/{email}/{Uri.EscapeDataString(name)}");

    /// <summary>
    /// Payload shape each array element binds: <c>ServiceEntity</c>'s wire fields, no id.
    /// <c>feeType</c> serializes as its NUMERIC value — Services.Api registers no
    /// <c>JsonStringEnumConverter</c>, so <see cref="FeeType"/> binds/emits as a plain int
    /// (0=Hourly, 1=Fixed, 2=Subscription), same as <see cref="AppointmentStatus"/> would if
    /// <see cref="BookingRouteBuilder.BuildUpdateStatusPayload"/> didn't deliberately send it as a string instead.
    /// </summary>
    public static object BuildServicePayload(string name, string description, decimal? fee, FeeType feeType, bool isActive, int? durationMinutes, string? professionName) =>
        new { name, description, fee, feeType = (int)feeType, isActive, durationMinutes, professionName };
}
