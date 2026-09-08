namespace AgendaBuddy.Library.Services;

public class ProviderService(IRepository<ProviderEntity> providerRepository) : IProviderService
{
    public async Task<IEnumerable<ProviderEntity>> GetAllProvidersAsync()
    {
        return await providerRepository.GetAllAsync();
    }

    /// <summary>
    /// One page of providers, plus the total number of them. ADR-023.
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

    public async Task<(IEnumerable<ProviderEntity> Items, long TotalCount)> GetPagedBookableProvidersAsync(
        int skip, int take)
    {
        // $elemMatch so BOTH conditions must hold on the SAME service. Without it Mongo would match a
        // provider having one active-but-unclassified service and a separate classified-but-inactive one,
        // neither of which is bookable.
        //
        // $ne/$nin rather than equality, because both fields are omitted from older documents:
        // isActive defaults to true in code and profession_name postdates the services already stored,
        // so "missing" has to be read as active and as unclassified respectively.
        var bookable = new BsonDocument("services", new BsonDocument("$elemMatch", new BsonDocument
        {
            { "isActive", new BsonDocument("$ne", false) },
            { "profession_name", new BsonDocument("$nin", new BsonArray { BsonNull.Value, "" }) }
        }));

        return await providerRepository.GetPagedAsync(bookable, skip, take);
    }

    public async Task<ProviderEntity> FindProvidersAsync(BsonDocument filter)
    {
        return await providerRepository.Find(filter);
    }

    public async Task<List<AppointmentEntity>> FindAppointmentsByCustomerAsync(string customerEmail)
    {
        // Dot notation into the embedded array matches a document when ANY element matches, so this
        // selects only the providers holding at least one appointment with this customer -- but those
        // providers' OTHER customers' appointments come back in the same documents, hence the second
        // filter below. Dropping it would leak every co-customer of every provider this customer books.
        var filter = new BsonDocument("appointments.email_customer", customerEmail);
        var providers = await providerRepository.FindAllAsync(filter);

        return providers
            .SelectMany(provider => provider.AppointmentEntities)
            .Where(appointment => string.Equals(appointment.EmailCustomer, customerEmail, StringComparison.OrdinalIgnoreCase))
            .OrderBy(appointment => appointment.Start)
            .ToList();
    }

    /// <summary>
    /// Appends an appointment to a provider's embedded list with a single atomic <c>$push</c>.
    /// </summary>
    /// <returns>The updated provider, or <c>null</c> when no provider has that email.</returns>
    /// <remarks>
    /// <para>
    /// ADR D-9. This replaces a read-append-replace: the booking handler used to load
    /// the provider, add to <see cref="ProviderEntity.AppointmentEntities"/>, and call
    /// <see cref="UpdateProviderAsync"/>, which is a <c>ReplaceOneAsync</c>. **Two concurrent bookings for
    /// one provider both read, both append, and the second replacement silently discards the first
    /// appointment** — which then exists in the `appointments` collection and not in the provider document,
    /// so the two disagree. `ReportingService` reads the embedded copy, so the lost booking is the one that
    /// disappears from the dashboard.
    /// </para>
    /// <para>
    /// <c>$push</c> has no read, so there is no window. The primitive it uses is backed by ADR-032.
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
    /// <c>DeactivateProviderCommandHandler</c> set <c>IsActive</c> on a loaded document
    /// and called <see cref="UpdateProviderAsync"/> — a whole-document replacement that would discard any
    /// appointment booked between the read and the write. It had never run, because nothing dispatched the
    /// command; now that the command is reachable, the race stops being theoretical.
    /// </remarks>
    public async Task<ProviderEntity?> SetActiveAsync(string providerEmail, bool isActive)
    {
        return await providerRepository.FindOneAndUpdateAsync(
            new BsonDocument("email", providerEmail),
            new BsonDocument("$set", new BsonDocument("is_active", isActive)));
    }

    public async Task<ProviderEntity?> SubscribeCustomerAsync(string providerEmail, string customerEmail)
    {
        return await providerRepository.FindOneAndUpdateAsync(
            new BsonDocument("email", providerEmail),
            new BsonDocument("$addToSet", new BsonDocument("subscribed_customer_collection", customerEmail)));
    }

    public async Task<ProviderEntity?> UnsubscribeCustomerAsync(string providerEmail, string customerEmail)
    {
        return await providerRepository.FindOneAndUpdateAsync(
            new BsonDocument("email", providerEmail),
            new BsonDocument("$pull", new BsonDocument("subscribed_customer_collection", customerEmail)));
    }

    public async Task<ProviderEntity?> AddProfessionsAsync(string providerEmail, List<string> professionNames)
    {
        return await providerRepository.FindOneAndUpdateAsync(
            new BsonDocument("email", providerEmail),
            new BsonDocument("$addToSet",
                new BsonDocument("professions", new BsonDocument("$each", new BsonArray(professionNames)))));
    }

    public async Task<ProviderEntity?> RemoveProfessionAsync(string providerEmail, string professionName)
    {
        return await providerRepository.FindOneAndUpdateAsync(
            new BsonDocument("email", providerEmail),
            new BsonDocument("$pull", new BsonDocument("professions", professionName)));
    }

    public async Task<ProviderEntity?> SetWorkHoursAsync(string providerEmail, int startHour, int endHour)
    {
        return await providerRepository.FindOneAndUpdateAsync(
            new BsonDocument("email", providerEmail),
            new BsonDocument("$set", new BsonDocument
            {
                { "work_day_start_hour", startHour },
                { "work_day_end_hour", endHour }
            }));
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <b>Read-merge-write on the array, deliberately, and it is not the lost-update shape ADR D-9 removed.</b>
    /// What that ADR is about is replacing a WHOLE DOCUMENT after reading it, which silently discards a
    /// concurrent edit to any other field. This reads only <c>work_week</c> and writes back only
    /// <c>work_week</c>, so a concurrent edit to services, appointments or professions is untouched. Two
    /// providers cannot race here at all — a provider's own calendar settings have exactly one writer.
    /// <para>
    /// The merge is what makes a partial week coherent: a request naming Monday and Tuesday must not clear
    /// Wednesday. MongoDB has no "upsert one element of an array by key" primitive — a positional <c>$set</c>
    /// needs the element to exist already, and <c>$addToSet</c> would append a second entry for a weekday
    /// rather than replace it, which is exactly the duplicate the calculator then has to resolve.
    /// </para>
    /// </remarks>
    public async Task<ProviderEntity?> SetWorkWeekAsync(string providerEmail, List<WorkDayHours> days)
    {
        if (days is null || days.Count == 0) return null;

        var provider = await providerRepository.FindOneAsync(new BsonDocument("email", providerEmail));
        if (provider is null) return null;

        // Incoming days win; everything else the provider already had is kept.
        var incoming = days.ToDictionary(day => day.Day);
        var merged = (provider.WorkWeek ?? [])
            .Where(existing => !incoming.ContainsKey(existing.Day))
            .Concat(days)
            .OrderBy(day => day.Day)
            .ToList();

        var serialised = new BsonArray(merged.Select(day =>
        {
            var document = new BsonDocument
            {
                { "day", (int)day.Day },
                { "is_closed", day.IsClosed }
            };

            // Omitted rather than written as null, matching [BsonIgnoreIfNull] on the entity — so a closed day
            // that never had hours carries no keys for them.
            if (day.StartHour.HasValue) document.Add("start_hour", day.StartHour.Value);
            if (day.EndHour.HasValue) document.Add("end_hour", day.EndHour.Value);

            return document;
        }));

        return await providerRepository.FindOneAndUpdateAsync(
            new BsonDocument("email", providerEmail),
            new BsonDocument("$set", new BsonDocument("work_week", serialised)));
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
        string providerEmail, string identifier, AppointmentStatus status, string description,
        bool clearProposal = false)
    {
        var update = new BsonDocument("$set", new BsonDocument
        {
            { "appointments.$.appointment_status", (int)status },
            { "appointments.$.appointment_description", description }
        });

        // Auto-completion needs this: a session that had a proposal outstanding when its time passed is completed
        // with the proposal dropped, and a Completed row still carrying a proposed time reads as an outstanding
        // request against a status saying the session is over.
        if (clearProposal)
        {
            update.Add("$unset", new BsonDocument
            {
                { "appointments.$.proposed_start", "" },
                { "appointments.$.proposed_by", "" }
            });
        }

        return await providerRepository.FindOneAndUpdateAsync(
            new BsonDocument
            {
                { "email", providerEmail },
                { "appointments.identifier", identifier }
            },
            update);
    }

    /// <summary>
    /// Moves the provider's embedded copy of an appointment to new times.
    /// </summary>
    /// <remarks>
    /// A positional <c>$set</c> for the same reason its status sibling is one: replacing the whole provider
    /// document to change one embedded appointment is the lost-update shape ADR D-9 removed from booking, and it
    /// would silently discard a concurrent edit to the provider's services or hours.
    /// <para>
    /// The embedded copy is what <c>AvailabilityCalculator</c> reads, so a reschedule that updates only the
    /// <c>appointments</c> collection leaves the OLD slot blocked and the new one still on offer — a
    /// double-booking generator.
    /// </para>
    /// </remarks>
    public async Task<ProviderEntity?> ChangeEmbeddedAppointmentScheduleAsync(
        string providerEmail, string identifier, DateTime startUtc, DateTime endUtc, DateTime? previousStartUtc = null)
    {
        var set = new BsonDocument
        {
            { "appointments.$.start", DateTime.SpecifyKind(startUtc.ToUniversalTime(), DateTimeKind.Utc) },
            { "appointments.$.end", DateTime.SpecifyKind(endUtc.ToUniversalTime(), DateTimeKind.Utc) },
            { "appointments.$.appointment_status", (int)AppointmentStatus.Booked },
            {
                "appointments.$.appointment_description",
                EnumHelper<AppointmentStatus>.GetEnumDescription(AppointmentStatus.Booked)
            }
        };

        if (previousStartUtc is { } previous)
        {
            set.Add(
                "appointments.$.previous_start",
                DateTime.SpecifyKind(previous.ToUniversalTime(), DateTimeKind.Utc));
        }

        return await providerRepository.FindOneAndUpdateAsync(
            new BsonDocument
            {
                { "email", providerEmail },
                { "appointments.identifier", identifier }
            },
            new BsonDocument
            {
                { "$set", set },
                {
                    "$unset", new BsonDocument
                    {
                        { "appointments.$.proposed_start", "" },
                        { "appointments.$.proposed_by", "" }
                    }
                }
            });
    }

    /// <summary>
    /// Records a reschedule proposal on the provider's embedded copy.
    /// </summary>
    /// <remarks>
    /// <b>Needed because the embedded list IS the client-facing read.</b>
    /// <c>GET /api/v1/calendar/appointments/{email}</c> serves <see cref="ProviderEntity.AppointmentEntities"/>
    /// — for a customer as well, since <c>CustomerEntity</c> holds only identifier strings — so a proposal
    /// written to the <c>appointments</c> collection alone reaches no screen at all. The status would arrive as
    /// <c>RescheduleRequested</c> with no proposed time, which every reader treats as "no proposal outstanding":
    /// the banner would never draw and Approve/Decline would never appear.
    /// </remarks>
    public async Task<ProviderEntity?> SetEmbeddedRescheduleProposalAsync(
        string providerEmail, string identifier, DateTime proposedStartUtc, string proposedBy)
    {
        return await providerRepository.FindOneAndUpdateAsync(
            new BsonDocument
            {
                { "email", providerEmail },
                { "appointments.identifier", identifier }
            },
            new BsonDocument("$set", new BsonDocument
            {
                { "appointments.$.appointment_status", (int)AppointmentStatus.RescheduleRequested },
                {
                    "appointments.$.appointment_description",
                    EnumHelper<AppointmentStatus>.GetEnumDescription(AppointmentStatus.RescheduleRequested)
                },
                {
                    "appointments.$.proposed_start",
                    DateTime.SpecifyKind(proposedStartUtc.ToUniversalTime(), DateTimeKind.Utc)
                },
                { "appointments.$.proposed_by", proposedBy }
            }));
    }

    /// <summary>
    /// Clears a proposal from the embedded copy and returns it to <c>Booked</c>, leaving its times alone.
    /// </summary>
    /// <remarks>
    /// For a DECLINE. Setting the status back without unsetting the proposal fields would leave a
    /// <c>Booked</c> appointment still carrying a proposed time, which reads as an outstanding request against a
    /// status that says there is none.
    /// </remarks>
    public async Task<ProviderEntity?> ClearEmbeddedRescheduleProposalAsync(
        string providerEmail, string identifier)
    {
        return await providerRepository.FindOneAndUpdateAsync(
            new BsonDocument
            {
                { "email", providerEmail },
                { "appointments.identifier", identifier }
            },
            new BsonDocument
            {
                {
                    "$set", new BsonDocument
                    {
                        { "appointments.$.appointment_status", (int)AppointmentStatus.Booked },
                        {
                            "appointments.$.appointment_description",
                            EnumHelper<AppointmentStatus>.GetEnumDescription(AppointmentStatus.Booked)
                        }
                    }
                },
                {
                    "$unset", new BsonDocument
                    {
                        { "appointments.$.proposed_start", "" },
                        { "appointments.$.proposed_by", "" }
                    }
                }
            });
    }

    public async Task<ProviderEntity?> SetAvatarAsync(string providerEmail, string avatarId)
    {
        return await providerRepository.FindOneAndUpdateAsync(
            new BsonDocument("email", providerEmail),
            new BsonDocument("$set", new BsonDocument("avatar_id", avatarId)));
    }

    public async Task<ProviderEntity?> SetConsentAsync(
        string providerEmail, DateTime? termsAcceptedAt, DateTime? privacyAcceptedAt)
    {
        return await providerRepository.FindOneAndUpdateAsync(
            new BsonDocument("email", providerEmail),
            new BsonDocument("$set", new BsonDocument
            {
                { "terms_accepted_at", ConsentTimestamp(termsAcceptedAt) },
                { "privacy_accepted_at", ConsentTimestamp(privacyAcceptedAt) }
            }));
    }

    /// <summary>
    /// A consent timestamp as BSON — an explicit null for "not accepted", not an omitted field, so an unticked
    /// box cannot leave a previous acceptance standing. See <c>CustomerService.ConsentTimestamp</c>.
    /// </summary>
    private static BsonValue ConsentTimestamp(DateTime? at) =>
        at is null ? BsonNull.Value : new BsonDateTime(at.Value);
}
