namespace Booking.Domain.Responses;

/// <summary>
/// The response envelope for all 10 of Booking's routes (ADR-049). In-repo, not a package type —
/// <c>SmallApiToolkit</c> was found not to ship this at all (PRD Requirement 10). Serialization
/// through <see cref="ObjectIdJsonConverter"/> for a nested <c>ObjectId</c>-backed field is verified
/// generically by F-019-T01 (<c>Library.Tests/Tools/ObjectIdJsonConverterTest.cs</c>), not repeated
/// here against this exact type.
/// </summary>
public sealed record DataResponse<T>(T? Data, IReadOnlyList<string> Errors)
{
    public bool Success => Errors.Count == 0;

    public static DataResponse<T> Ok(T data) => new(data, []);

    public static DataResponse<T> Fail(IEnumerable<string> errors) => new(default, errors.ToList());
}
