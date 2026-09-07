namespace AgendaBuddy.MobileApp.Models;

/// <summary>
/// A period the provider is unavailable, as this device reads it.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Start"/> and <see cref="End"/> are LOCAL, converted on the way in, because a provider setting their
/// own time off is thinking in their own clock — and because they are rendered directly. The identifier is what
/// goes back to the server for a removal; <c>ObjectId</c> cannot be round-tripped through JSON without the
/// custom converter, which is why blocks carry their own.
/// </para>
/// <para>
/// <see cref="Reason"/> is the provider's private note. It arrives only on the owner-guarded blocks route and must
/// never be rendered anywhere a customer can see.
/// </para>
/// </remarks>
public sealed record CalendarBlock(string Identifier, DateTime Start, DateTime End, string? Reason)
{
    public bool HasReason => !string.IsNullOrWhiteSpace(Reason);

    /// <summary>Whether the block covers whole days rather than part of one.</summary>
    public bool IsAllDay => Start.TimeOfDay == TimeSpan.Zero && End.TimeOfDay == TimeSpan.Zero;

    /// <summary>Whether it spans more than one date.</summary>
    public bool IsMultiDay => End.Date > Start.Date && !(IsAllDay && End.Date == Start.Date.AddDays(1));

    /// <summary>
    /// The range in words, shaped by what it actually is — a single day off, an afternoon, or a trip.
    /// </summary>
    /// <remarks>
    /// A single format for all three reads badly: "8 Sep 00:00 – 9 Sep 00:00" is how a day off would otherwise
    /// render, which nobody would describe that way.
    /// </remarks>
    public string RangeLabel
    {
        get
        {
            if (IsAllDay && End.Date == Start.Date.AddDays(1))
                return $"{Start:ddd d MMM} · all day";

            if (IsAllDay)
                return $"{Start:ddd d MMM} – {End.AddDays(-1):ddd d MMM} · all day";

            if (IsMultiDay)
                return $"{Start:ddd d MMM, h:mm tt} – {End:ddd d MMM, h:mm tt}";

            return $"{Start:ddd d MMM} · {Start:h:mm tt} – {End:h:mm tt}";
        }
    }
}
