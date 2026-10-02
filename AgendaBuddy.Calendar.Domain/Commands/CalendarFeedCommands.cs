using AgendaBuddy.Library.Services;

namespace AgendaBuddy.Calendar.Domain.Commands;

/// <summary>
/// Issues the caller's calendar subscription token, replacing any previous one so every earlier URL stops working.
/// </summary>
[ExcludeFromCodeCoverage]
public class EnableCalendarFeedCommand : IRequest<Result<EnabledCalendarFeed>>
{
    public required string OwnerEmail { get; set; }

    /// <summary>The language event titles are written in; anything not Spanish is English.</summary>
    public string? Language { get; set; }
}

/// <summary>Turns the caller's calendar feed off. Idempotent.</summary>
[ExcludeFromCodeCoverage]
public class DisableCalendarFeedCommand : IRequest<Result<bool>>
{
    public required string OwnerEmail { get; set; }
}
