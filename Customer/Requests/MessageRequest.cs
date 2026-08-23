namespace Customer.Requests;

/// <summary>
/// A message's recipient and body.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>There is no sender field, deliberately.</b> The sender is the caller's <c>sub</c> claim (threat
/// T-204). Omitting it from the request type is a stronger guarantee than validating it, because there is
/// nothing for a later refactor to start trusting — and it means the API cannot be probed for whether the
/// field is inspected.
/// </para>
/// <para>
/// The field is <c>Body</c>, not <c>Content</c>: <c>MessageEntity</c> stores <c>[BsonElement("body")]</c>
/// while <c>NoteEntity</c> stores <c>[BsonElement("content")]</c>. The two are inconsistent and F-014 does
/// not rename either — a rename is a data migration for no functional gain. Named here to match the entity
/// rather than to be tidy, because the contract has to describe what is stored.
/// </para>
/// </remarks>
public record MessageRequest(string RecipientEmail, string Body);
