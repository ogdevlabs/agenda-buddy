namespace AgendaBuddy.Customer.Requests;

/// <summary>
/// A message's recipient and body.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>There is no sender field, deliberately.</b> The sender is the caller's <c>sub</c> claim.
/// Omitting it from the request type is a stronger guarantee than validating it, because there is
/// nothing for a later refactor to start trusting — and it means the API cannot be probed for whether the
/// field is inspected.
/// </para>
/// <para>
/// The field is <c>Body</c>, not <c>Content</c>: <c>MessageEntity</c> stores <c>[BsonElement("body")]</c>
/// while <c>NoteEntity</c> stores <c>[BsonElement("content")]</c>. The two are inconsistent and neither is
/// renamed here — a rename is a data migration for no functional gain. Named here to match the entity
/// rather than to be tidy, because the contract has to describe what is stored.
/// </para>
/// <para>
/// This route (<c>POST /api/v1/messages</c>) never went through MediatR/<c>Result&lt;T&gt;</c> —
/// it calls <c>IMessageService.SendMessageAsync</c> directly, matching Provider's own
/// <c>GetProviderReport</c> precedent (a route this service's Clean Architecture split deliberately
/// leaves untouched). This type is a real, live request DTO bound straight from the endpoint body —
/// not a leftover from a deleted handler.
/// </para>
/// </remarks>
public record MessageRequest(string RecipientEmail, string Body);
