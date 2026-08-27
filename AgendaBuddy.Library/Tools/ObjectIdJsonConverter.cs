using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgendaBuddy.Library.Tools;

/// <summary>
/// Serialises a MongoDB <see cref="ObjectId"/> as its 24-character hex string, and reads one back.
/// </summary>
/// <remarks>
/// <para>
/// Without this, <c>System.Text.Json</c> has no idea what an <see cref="ObjectId"/> is, so it serialises the
/// struct's public properties and emits
/// </para>
/// <code>
/// "id": { "timestamp": 1787455661, "machine": 12345, "pid": 678, "increment": 90, "creationTime": "…" }
/// </code>
/// <para>
/// — which cannot be deserialised back into an <see cref="ObjectId"/> at all (it has no settable properties),
/// so a client reading its own response gets <c>ObjectId.Empty</c>. That is fatal for three route
/// families: <c>PUT /api/v1/booking/notes/{id}</c>, <c>POST /api/v1/messages/{id}/read</c> and
/// <c>POST /api/v1/notifications/{id}/read</c> all need the id the create response returned.
/// </para>
/// <para>
/// ⚠️ <b>This is a pre-existing defect.</b> Every route that returns
/// <c>ProviderEntity</c>, <c>CustomerEntity</c>, <c>ServiceEntity</c> or <c>ProfessionEntity</c> has been
/// emitting the same unusable shape since those routes were written — nothing noticed, because the mobile
/// client could not reach any of them and no test read an <c>id</c> back. This converter is registered only
/// in the services whose routes need it to work; the rest are filed rather than changed here, since altering
/// their response shape is out of scope.
/// </para>
/// <para>
/// <c>Identity</c> needs none of this: <c>CredentialEntity.Id</c> is a <c>string</c> carrying
/// <c>[BsonRepresentation(BsonType.ObjectId)]</c>, which is the pattern every entity should arguably have
/// used.
/// </para>
/// </remarks>
public sealed class ObjectIdJsonConverter : JsonConverter<ObjectId>
{
    public override ObjectId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Tolerant on the way in: an absent or explicitly null id reads as Empty rather than throwing, because
        // a create request legitimately has no id yet and a 400 for omitting one would be a trap.
        if (reader.TokenType is JsonTokenType.Null) return ObjectId.Empty;

        var value = reader.GetString();
        return string.IsNullOrWhiteSpace(value) ? ObjectId.Empty : ObjectId.Parse(value);
    }

    public override void Write(Utf8JsonWriter writer, ObjectId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}
