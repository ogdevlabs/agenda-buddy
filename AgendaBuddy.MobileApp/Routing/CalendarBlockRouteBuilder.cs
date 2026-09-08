namespace AgendaBuddy.MobileApp.Routing;

/// <summary>
/// Route-building for a provider's time-off blocks.
/// </summary>
/// <remarks>
/// Every route here is OWNER-ONLY on the server, and that is stronger than it looks: a block's reason is the
/// provider's private note. The customer-facing availability route already reflects blocks — as absence, which is
/// all a customer needs and all they get.
/// <para>
/// No Gateway allowlist entry was needed: <c>/api/v1/calendar/{**catch-all}</c> already covers these paths. That
/// is worth knowing, because a NEW top-level group would not have been (messages and notifications each needed
/// their own line, and were unreachable through the Gateway until they got one).
/// </para>
/// </remarks>
public static class CalendarBlockRouteBuilder
{
    public static RouteSpec GetBlocks(string email) =>
        new(HttpMethod.Get, $"api/v1/calendar/blocks/{email}");

    public static RouteSpec CreateBlock(string email) =>
        new(HttpMethod.Post, $"api/v1/calendar/blocks/{email}");

    public static RouteSpec RemoveBlock(string email, string identifier) =>
        new(HttpMethod.Delete, $"api/v1/calendar/blocks/{email}/{identifier}");

    /// <summary>
    /// What a proposed range would strand, read BEFORE creating a block so the cost is visible in advance.
    /// </summary>
    /// <remarks>
    /// The instants go on the query string in round-trip ("o") format, so neither the device's culture nor its
    /// offset can reinterpret them between here and the server.
    /// </remarks>
    public static RouteSpec BlockConflicts(string email, DateTime startUtc, DateTime endUtc) =>
        new(HttpMethod.Get,
            $"api/v1/calendar/blocks/{email}/conflicts"
            + $"?startUtc={Uri.EscapeDataString(startUtc.ToString("o"))}"
            + $"&endUtc={Uri.EscapeDataString(endUtc.ToString("o"))}");

    /// <summary>
    /// Payload shape Calendar's <c>CalendarBlockRequest</c> binds.
    /// </summary>
    /// <remarks>
    /// UTC instants, not local: the provider picks in their own clock and this is where that is converted, once.
    /// <paramref name="force"/> defaults to false so a range with live sessions in it is refused with a count
    /// rather than silently blocking over them.
    /// </remarks>
    public static object BuildBlockPayload(DateTime startUtc, DateTime endUtc, string? reason, bool force) =>
        new { startUtc, endUtc, reason, force };
}
