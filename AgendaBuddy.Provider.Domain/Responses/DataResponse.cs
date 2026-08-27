namespace AgendaBuddy.Provider.Domain.Responses;

/// <summary>
/// The response envelope for Provider's routes (F-020, following ADR-049's Booking precedent).
/// In-repo, not a package type -- see <c>AgendaBuddy.Booking.Domain.Responses.DataResponse</c> for the
/// original rationale.
/// </summary>
/// <remarks>
/// <c>GetProviderReport</c> is deliberately NOT wrapped in this envelope: it never went through
/// MediatR/<c>Result&lt;T&gt;</c> (it calls <c>IReportingService</c> directly), and
/// <c>ReportAndDeactivationTest</c> deserialises its body at the root
/// (<c>ReadFromJsonAsync&lt;ProviderReport&gt;</c>) and reads <c>revenueAvailable</c>/
/// <c>revenueUnavailableReason</c> at the root too. Wrapping it would be a real behaviour change to a
/// route this task's recipe never touched.
/// </remarks>
public sealed record DataResponse<T>(T? Data, IReadOnlyList<string> Errors)
{
    public bool Success => Errors.Count == 0;

    public static DataResponse<T> Ok(T data) => new(data, []);

    public static DataResponse<T> Fail(IEnumerable<string> errors) => new(default, errors.ToList());
}
