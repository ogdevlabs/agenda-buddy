namespace AgendaBuddy.Customer.Domain.Responses;

/// <summary>
/// The response envelope for Customer's CQRS routes (F-020, following ADR-049's Booking precedent).
/// In-repo, not a package type -- see <c>AgendaBuddy.Booking.Domain.Responses.DataResponse</c> for the
/// original rationale.
/// </summary>
/// <remarks>
/// The messages/notifications routes hosted by this service are deliberately NOT wrapped in this
/// envelope: they never went through MediatR/<c>Result&lt;T&gt;</c> (they call <c>IMessageService</c>/
/// <c>INotificationService</c> directly), matching <c>AgendaBuddy.Provider.Domain.Responses.DataResponse</c>'s
/// own <c>GetProviderReport</c> precedent. Wrapping them would be a real behaviour change to routes this
/// task's recipe never touched.
/// </remarks>
public sealed record DataResponse<T>(T? Data, IReadOnlyList<string> Errors)
{
    public bool Success => Errors.Count == 0;

    public static DataResponse<T> Ok(T data) => new(data, []);

    public static DataResponse<T> Fail(IEnumerable<string> errors) => new(default, errors.ToList());
}
