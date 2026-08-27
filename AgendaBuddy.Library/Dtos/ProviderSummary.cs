using AgendaBuddy.Library.Entities;

namespace AgendaBuddy.Library.Dtos;

/// <summary>
/// A provider as seen by anyone who is not that provider: public profile and service catalogue only.
/// </summary>
/// <remarks>
/// <para>
/// F-016 AC-9 / requirement 10 (F-016-T11). <c>ProviderEntity</c> embeds <c>ServiceEntities</c>,
/// <c>AppointmentEntities</c> — each carrying <c>email_customer</c> — and <c>SubscribedCustomerCollection</c>
/// (<c>ProviderEntity.cs:38-42</c>). Authentication alone does not fix that: an authenticated <em>customer</em>
/// browsing for a coach would still receive every provider's appointment book and client roster. Requirement
/// 10 therefore holds regardless of the authentication decision.
/// </para>
/// <para>
/// <b>Deliberately absent:</b> <c>AppointmentEntities</c>, <c>SubscribedCustomerCollection</c>,
/// <c>KafkaTopic</c>, <c>Id</c>. Absence is by construction — this type has no field to populate — rather
/// than by a filtering step someone could forget.
/// </para>
/// <para>
/// <b>A read-boundary projection, not a schema change.</b> The stored document keeps its embedded shape;
/// restructuring it is a migration and belongs to F-019/F-020.
/// </para>
/// <para>
/// ⚠️ <b>This is the first DTO folder in a codebase whose endpoints return entities directly.</b> The
/// pattern is introduced here on purpose rather than appearing by accident, and F-019/F-020 will generalise
/// it (<c>ARCHITECTURE.md</c> §3.3).
/// </para>
/// <para>
/// ⚠️ <b>No <c>profession</c> field, contrary to <c>api-contracts.md</c> §5.1.</b> That example shows
/// <c>"profession": "Fitness Coach"</c> and a <c>"duration"</c> on each service. Neither exists:
/// <c>ProviderEntity</c> has no profession property, and <c>ServiceEntity</c> has <c>Name</c>,
/// <c>Description</c>, <c>Fee</c>, <c>FeeType</c> and <c>IsActive</c> but no duration. Verified by reading
/// both entities. Corrected in <c>api-contracts.md</c>, because F-015 is written against that document and
/// would otherwise bind to fields that do not exist.
/// </para>
/// </remarks>
public sealed class ProviderSummary
{
    public required string Email { get; init; }

    public required string FirstName { get; init; }

    public required string LastName { get; init; }

    /// <summary>The provider's service catalogue — what a customer needs in order to choose one.</summary>
    public required List<ServiceEntity> Services { get; init; }

    /// <summary>Projects a stored provider to the shape a non-owner may see.</summary>
    public static ProviderSummary From(ProviderEntity provider) => new()
    {
        Email = provider.Email,
        FirstName = provider.FirstName,
        LastName = provider.LastName,
        Services = provider.ServiceEntities,
    };
}
