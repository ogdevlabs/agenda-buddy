using System.Text.Json;
using Library.Tools;

namespace AgendaBuddy.IntegrationTests.Harness;

/// <summary>
/// The JSON options a client of these services needs.
/// </summary>
/// <remarks>
/// <para>
/// Web defaults (camelCase) plus <see cref="ObjectIdJsonConverter"/>. The converter is not optional: every
/// entity in this product carries a MongoDB <c>ObjectId</c> as its <c>id</c>, and while F-014 made the
/// <b>server</b> write it as a hex string, a client still needs to know how to read one back into an
/// <c>ObjectId</c>.
/// </para>
/// <para>
/// ⚠️ <b>Carry this to F-015.</b> The mobile client will hit exactly this and the failure is unhelpful —
/// <c>"The JSON value could not be converted to MongoDB.Bson.ObjectId"</c>, thrown by the client, on a
/// response the server produced correctly. The cheaper long-term answer is for entities to declare
/// <c>string Id</c> with <c>[BsonRepresentation(BsonType.ObjectId)]</c> as <c>CredentialEntity</c> already
/// does, which needs no converter on either side.
/// </para>
/// </remarks>
internal static class HarnessJson
{
    internal static readonly JsonSerializerOptions Options = Build();

    private static JsonSerializerOptions Build()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new ObjectIdJsonConverter());
        return options;
    }
}
