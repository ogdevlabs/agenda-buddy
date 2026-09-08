namespace AgendaBuddy.Library.Services;

public interface IProviderService
{
    Task<IEnumerable<ProviderEntity>> GetAllProvidersAsync();

    /// <summary>One page of providers, plus the total number of them. ADR-023.</summary>
    Task<(IEnumerable<ProviderEntity> Items, long TotalCount)> GetPagedProvidersAsync(int skip, int take);

    /// <summary>
    /// One page of providers a customer could actually book, plus how many there are.
    /// </summary>
    /// <remarks>
    /// <para>
    /// "Bookable" means the provider has at least one service that is active <b>and</b> classified under
    /// one of their professions. A provider with no services cannot be booked at all, and an unclassified
    /// service cannot be reached by a flow that selects a profession before a service — so listing either
    /// in the customer directory offers something that dead-ends.
    /// </para>
    /// <para>
    /// Matching and paging both happen in the database (see
    /// <c>IRepository.GetPagedAsync(BsonDocument,int,int)</c>): filtering a page after the fact would
    /// return short pages with a count that disagrees, and filtering a full read in memory is the
    /// full-dataset load ADR-023 exists to prevent.
    /// </para>
    /// </remarks>
    Task<(IEnumerable<ProviderEntity> Items, long TotalCount)> GetPagedBookableProvidersAsync(int skip, int take);

    Task<ProviderEntity> GetProviderByIdAsync(string id);
    Task AddProviderAsync(ProviderEntity provider);
    Task<bool> UpdateProviderAsync(string id, ProviderEntity provider);
    Task DeleteProviderAsync(string id);
    Task<ProviderEntity> FindProvidersAsync(BsonDocument filter);

    /// <summary>
    /// Every appointment booked with <paramref name="customerEmail"/>, gathered across all providers.
    /// </summary>
    /// <remarks>
    /// Appointments are embedded in each provider's own document
    /// (<see cref="ProviderEntity.AppointmentEntities"/>); a customer's
    /// <c>CustomerEntity.AppointmentCollection</c> holds bare identifier strings, not the appointments
    /// themselves. So a customer's own calendar cannot be answered by looking that customer up — it has
    /// to be gathered from the provider side, which is what this exists for.
    /// </remarks>
    Task<List<AppointmentEntity>> FindAppointmentsByCustomerAsync(string customerEmail);

    /// <summary>
    /// Flips a provider's active flag with a single targeted write. Added so
    /// DeactivateProviderCommandHandler can be typed against this interface rather than the concrete
    /// <see cref="ProviderService"/> class — the only two call sites (this one and
    /// <see cref="GetPagedProvidersAsync"/>) were the reason Provider's handlers could not move to
    /// interface typing without this addition.
    /// </summary>
    Task<ProviderEntity?> SetActiveAsync(string providerEmail, bool isActive);

    /// <summary>
    /// Adds <paramref name="customerEmail"/> to the provider's own <c>SubscribedCustomerCollection</c> —
    /// the reciprocal side of a customer's subscribe action, kept in sync via a targeted
    /// <c>$addToSet</c> (ADR-032). Also serves as the provider-existence check: a <c>null</c> return
    /// means <paramref name="providerEmail"/> matched no provider.
    /// </summary>
    Task<ProviderEntity?> SubscribeCustomerAsync(string providerEmail, string customerEmail);

    /// <summary>
    /// Removes <paramref name="customerEmail"/> from the provider's reciprocal subscriber list via a
    /// targeted <c>$pull</c>. A missing provider is not an error here — see the call site in
    /// <c>UnsubscribeFromProviderCommandHandler</c> for why cleanup on the customer's side must not be
    /// blocked by a since-deleted provider.
    /// </summary>
    Task<ProviderEntity?> UnsubscribeCustomerAsync(string providerEmail, string customerEmail);

    /// <summary>Adds to the provider's <c>professions</c> list via <c>$addToSet</c>/<c>$each</c> — a
    /// targeted update (ADR-032), so already-present names are silently deduplicated rather than erroring.</summary>
    Task<ProviderEntity?> AddProfessionsAsync(string providerEmail, List<string> professionNames);

    /// <summary>Removes one profession from the provider's list via a targeted <c>$pull</c>.</summary>
    Task<ProviderEntity?> RemoveProfessionAsync(string providerEmail, string professionName);

    /// <summary>
    /// Sets the provider's working-day bounds via a targeted <c>$set</c> (ADR-032) rather than replacing
    /// the document, so saving hours cannot disturb their services or appointments.
    /// </summary>
    /// <returns><c>null</c> when no provider matched, which also serves as the existence check.</returns>
    Task<ProviderEntity?> SetWorkHoursAsync(string providerEmail, int startHour, int endHour);

    /// <summary>
    /// Replaces the provider's per-weekday hours for the weekdays given, leaving the rest alone.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A targeted <c>$set</c> on <c>work_week</c>, for the same reason its single-pair sibling is one: a
    /// whole-document replace would discard a concurrent edit to the provider's services or appointments.
    /// </para>
    /// <para>
    /// <b>Merged, not overwritten.</b> A request naming Monday and Tuesday must not silently clear Wednesday —
    /// a partial week is a coherent request, and the single legacy pair remains the fallback for any weekday
    /// still absent.
    /// </para>
    /// </remarks>
    /// <returns><c>null</c> when no provider matched, which also serves as the existence check.</returns>
    Task<ProviderEntity?> SetWorkWeekAsync(string providerEmail, List<WorkDayHours> days);

    /// <summary>
    /// Writes a new status onto one appointment inside a provider's embedded list, via the positional
    /// <c>$</c> operator so only the matched element changes.
    /// </summary>
    /// <returns>
    /// The updated provider, or <c>null</c> when the provider does not exist or holds no appointment with that
    /// identifier.
    /// </returns>
    /// <remarks>
    /// On the interface (not just the concrete service) because cancellation needs it: cancelling is a soft
    /// delete, so it updates the embedded copy's status where it used to remove the element and replace the
    /// whole provider document. <c>ReportingService</c> counts statuses from the embedded list, so a status
    /// written to only the <c>appointments</c> collection would leave the dashboard reporting the old value.
    /// </remarks>
    /// <param name="clearProposal">
    /// Also unset any reschedule proposal on the embedded copy. Needed when completing a session that had one
    /// outstanding — a <c>Completed</c> row still carrying a proposed time reads as an outstanding request
    /// against a status saying the session is over.
    /// </param>
    Task<ProviderEntity?> ChangeEmbeddedAppointmentStatusAsync(
        string providerEmail, string identifier, AppointmentStatus status, string description,
        bool clearProposal = false);

    /// <summary>
    /// Moves the provider's embedded copy of an appointment to new times, clearing any proposal on it and
    /// returning it to <c>Booked</c>.
    /// </summary>
    /// <remarks>
    /// The embedded list is what <see cref="Tools.AvailabilityCalculator"/> reads for the busy set, so a
    /// reschedule that updates only the <c>appointments</c> collection leaves the OLD slot blocked and the new one
    /// still on offer — which is a double-booking generator, not a stale display.
    /// </remarks>
    Task<ProviderEntity?> ChangeEmbeddedAppointmentScheduleAsync(
        string providerEmail, string identifier, DateTime startUtc, DateTime endUtc,
        DateTime? previousStartUtc = null);

    /// <summary>
    /// Records a reschedule proposal on the provider's embedded copy.
    /// </summary>
    /// <remarks>
    /// <b>The embedded list is the client-facing read.</b>
    /// <c>GET /api/v1/calendar/appointments/{email}</c> serves it for BOTH roles, so a proposal written only to
    /// the <c>appointments</c> collection reaches no screen: the status arrives as <c>RescheduleRequested</c>
    /// carrying no proposed time, which every reader correctly treats as no proposal at all.
    /// </remarks>
    Task<ProviderEntity?> SetEmbeddedRescheduleProposalAsync(
        string providerEmail, string identifier, DateTime proposedStartUtc, string proposedBy);

    /// <summary>Clears a proposal from the embedded copy and returns it to <c>Booked</c>. For a decline.</summary>
    Task<ProviderEntity?> ClearEmbeddedRescheduleProposalAsync(string providerEmail, string identifier);
}
