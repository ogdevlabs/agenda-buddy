namespace AgendaBuddy.Calendar.Domain.Responses;

/// <summary>
/// The response envelope for both of Calendar's routes (F-020, following ADR-049's Booking
/// precedent). In-repo, not a package type -- see <c>AgendaBuddy.Booking.Domain.Responses.DataResponse</c>
/// for the original rationale.
/// </summary>
public sealed record DataResponse<T>(T? Data, IReadOnlyList<string> Errors)
{
    public bool Success => Errors.Count == 0;

    public static DataResponse<T> Ok(T data) => new(data, []);

    public static DataResponse<T> Fail(IEnumerable<string> errors) => new(default, errors.ToList());
}
