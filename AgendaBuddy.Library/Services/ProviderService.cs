namespace AgendaBuddy.Library.Services;

public class ProviderService(IRepository<ProviderEntity> providerRepository) : IProviderService
{
    public async Task<IEnumerable<ProviderEntity>> GetAllProvidersAsync()
    {
        return await providerRepository.GetAllAsync();
    }

    /// <summary>
    /// One page of providers, plus the total number of them. F-016-T15 / ADR-023.
    /// </summary>
    /// <remarks>
    /// Paged at the database, not after the fact. Reading everything and slicing in the endpoint would bound
    /// the RESPONSE while leaving the EXTRACTION unbounded, which is the opposite of the point.
    /// </remarks>
    public async Task<(IEnumerable<ProviderEntity> Items, long TotalCount)> GetPagedProvidersAsync(int skip, int take)
    {
        return await providerRepository.GetPagedAsync(skip, take);
    }

    public async Task<ProviderEntity> GetProviderByIdAsync(string id)
    {
        return await providerRepository.GetByIdAsync(id);
    }

    public async Task AddProviderAsync(ProviderEntity provider)
    {
        await providerRepository.InsertAsync(provider);
    }

    public async Task<bool> UpdateProviderAsync(string id, ProviderEntity provider)
    {
        var existingProvider = await providerRepository.GetByIdAsync(id);
        if (existingProvider == null) throw new ArgumentException("Provider not found");

        return await providerRepository.UpdateAsync(id, provider);
    }

    public async Task DeleteProviderAsync(string id)
    {
        await providerRepository.DeleteAsync(id);
    }

    public async Task<ProviderEntity> FindProvidersAsync(BsonDocument filter)
    {
        return await providerRepository.Find(filter);
    }

    /// <summary>
    /// Appends an appointment to a provider's embedded list with a single atomic <c>$push</c>.
    /// </summary>
    /// <returns>The updated provider, or <c>null</c> when no provider has that email.</returns>
    /// <remarks>
    /// <para>
    /// F-014 requirement 20 / ADR D-9. This replaces a read-append-replace: the booking handler used to load
    /// the provider, add to <see cref="ProviderEntity.AppointmentEntities"/>, and call
    /// <see cref="UpdateProviderAsync"/>, which is a <c>ReplaceOneAsync</c>. **Two concurrent bookings for
    /// one provider both read, both append, and the second replacement silently discards the first
    /// appointment** — which then exists in the `appointments` collection and not in the provider document,
    /// so the two disagree. `ReportingService` reads the embedded copy, so the lost booking is the one that
    /// disappears from the dashboard.
    /// </para>
    /// <para>
    /// <c>$push</c> has no read, so there is no window. The primitive it uses arrived with F-021 (ADR-032).
    /// </para>
    /// </remarks>
    public async Task<ProviderEntity?> AppendAppointmentAsync(string providerEmail, AppointmentEntity appointment)
    {
        return await providerRepository.FindOneAndUpdateAsync(
            new BsonDocument("email", providerEmail),
            new BsonDocument("$push", new BsonDocument("appointments", appointment.ToBsonDocument())));
    }

    /// <summary>
    /// Flips a provider's active flag with a single targeted write.
    /// </summary>
    /// <returns>The updated provider, or <c>null</c> when no provider has that email.</returns>
    /// <remarks>
    /// F-014 requirement 20. <c>DeactivateProviderCommandHandler</c> set <c>IsActive</c> on a loaded document
    /// and called <see cref="UpdateProviderAsync"/> — a whole-document replacement that would discard any
    /// appointment booked between the read and the write. It had never run, because nothing dispatched the
    /// command; F-014 makes it reachable, so it stops being theoretical.
    /// </remarks>
    public async Task<ProviderEntity?> SetActiveAsync(string providerEmail, bool isActive)
    {
        return await providerRepository.FindOneAndUpdateAsync(
            new BsonDocument("email", providerEmail),
            new BsonDocument("$set", new BsonDocument("is_active", isActive)));
    }

    /// <summary>
    /// Writes a new status onto one appointment inside a provider's embedded list.
    /// </summary>
    /// <returns>
    /// The updated provider, or <c>null</c> when the provider does not exist or holds no appointment with
    /// that identifier.
    /// </returns>
    /// <remarks>
    /// Uses the positional <c>$</c> operator, so it updates the matched array element and only that element.
    /// The embedded copy has to be updated as well as the `appointments` collection because
    /// <c>ReportingService</c> counts statuses from the <b>embedded</b> list — a status written to only one
    /// of the two places would leave the dashboard reporting the old value indefinitely.
    /// </remarks>
    public async Task<ProviderEntity?> ChangeEmbeddedAppointmentStatusAsync(
        string providerEmail, string identifier, AppointmentStatus status, string description)
    {
        return await providerRepository.FindOneAndUpdateAsync(
            new BsonDocument
            {
                { "email", providerEmail },
                { "appointments.identifier", identifier }
            },
            new BsonDocument("$set", new BsonDocument
            {
                { "appointments.$.appointment_status", (int)status },
                { "appointments.$.appointment_description", description }
            }));
    }
}
