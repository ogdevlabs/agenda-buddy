namespace AgendaBuddy.Gateway;

/// <summary>
/// Anchor type for <c>WebApplicationFactory&lt;TEntryPoint&gt;</c> in the integration harness.
/// </summary>
/// <remarks>
/// <c>Program.cs</c> uses top-level statements, so it emits an internal, ambiguous <c>Program</c> type —
/// see <c>AgendaBuddy.IntegrationTests/Harness/EntryPoints.cs</c> for why every service is hosted through
/// a distinct public type instead. The seven domain services each already expose a
/// <c>*.Configuration(s).MongoDbConfiguration</c> type that serves this purpose incidentally; Gateway has
/// no MongoDB configuration to reuse, so this type exists solely to be public and to live in the Gateway
/// assembly.
/// </remarks>
public sealed class GatewayAnchor
{
    private GatewayAnchor()
    {
    }
}
