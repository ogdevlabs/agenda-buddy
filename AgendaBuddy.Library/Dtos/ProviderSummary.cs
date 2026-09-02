using AgendaBuddy.Library.Entities;

namespace AgendaBuddy.Library.Dtos;

/// <summary>
/// A provider as seen by anyone who is not that provider: public profile and service catalogue only.
/// </summary>
/// <remarks>
/// <para>
/// AC-9. <c>ProviderEntity</c> embeds <c>ServiceEntities</c>,
/// <c>AppointmentEntities</c> — each carrying <c>email_customer</c> — and <c>SubscribedCustomerCollection</c>
/// (<c>ProviderEntity.cs:38-42</c>). Authentication alone does not fix that: an authenticated <em>customer</em>
/// browsing for a coach would still receive every provider's appointment book and client roster. This
/// projection is required regardless of the authentication decision.
/// </para>
/// <para>
/// <b>Deliberately absent:</b> <c>AppointmentEntities</c>, <c>SubscribedCustomerCollection</c>,
/// <c>KafkaTopic</c>, <c>Id</c>. Absence is by construction — this type has no field to populate — rather
/// than by a filtering step someone could forget.
/// </para>
/// <para>
/// <b>A read-boundary projection, not a schema change.</b> The stored document keeps its embedded shape;
/// restructuring it is a separate migration.
/// </para>
/// <para>
/// ⚠️ <b>This is the first DTO folder in a codebase whose endpoints return entities directly.</b> The
/// pattern is introduced here on purpose rather than appearing by accident, and later work will generalise
/// it (<c>ARCHITECTURE.md</c> §3.3).
/// </para>
/// <para>
/// ⚠️ <b>No <c>profession</c> field, contrary to <c>api-contracts.md</c> §5.1.</b> That example shows
/// <c>"profession": "Fitness Coach"</c> and a <c>"duration"</c> on each service. Neither exists:
/// <c>ProviderEntity</c> has no profession property, and <c>ServiceEntity</c> has <c>Name</c>,
/// <c>Description</c>, <c>Fee</c>, <c>FeeType</c> and <c>IsActive</c> but no duration. Verified by reading
/// both entities. Corrected in <c>api-contracts.md</c>, since the mobile client is written against that
/// document and would otherwise bind to fields that do not exist.
/// </para>
/// </remarks>
public sealed class ProviderSummary
{
    public required string Email { get; init; }

    public required string FirstName { get; init; }

    public required string LastName { get; init; }

    /// <summary>The provider's service catalogue — what a customer needs in order to choose one.</summary>
    public required List<ServiceEntity> Services { get; init; }

    /// <summary>The provider's own Professions (<see cref="ProviderEntity.Professions"/>) — added
    /// 2026-08-29 alongside that field; this DTO's own remarks about a missing profession field predate it.</summary>
    public required List<string> Professions { get; init; }

    /// <summary>Projects a stored provider to the shape a non-owner may see.</summary>
    /// <remarks>
    /// <b>The whole service catalogue is exposed, deliberately.</b> Narrowing it to the "bookable" ones
    /// (active and classified under a profession) was tried and reverted: this is a general discovery
    /// projection whose contract is "services visible, appointments and customers not", and filtering here
    /// broke that — <c>ProviderProjectionTest</c> caught it. Bookability is filtered where it belongs
    /// instead: at the provider level by <c>bookableOnly=true</c> on the list route, and at the service
    /// level by the booking screen, which reads the catalogue from
    /// <c>GET /api/v1/services/{email}</c> and drops inactive/unclassified entries itself.
    /// </remarks>
    public static ProviderSummary From(ProviderEntity provider) => new()
    {
        Email = provider.Email,
        FirstName = provider.FirstName,
        LastName = provider.LastName,
        Services = provider.ServiceEntities ?? [],
        Professions = provider.Professions,
    };
}
