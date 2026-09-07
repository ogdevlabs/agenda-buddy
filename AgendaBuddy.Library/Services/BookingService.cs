namespace AgendaBuddy.Library.Services;

public class BookingService(IRepository<AppointmentEntity> appointmentRepository) : IBookingService
{
    public async Task BookAppointmentAsync(AppointmentEntity appointmentEntity)
    {
        await appointmentRepository.InsertAsync(appointmentEntity);
    }

    public async Task<bool> UpdateAppointmentAsync(string identifier, AppointmentEntity appointmentEntity)
    {
        return await appointmentRepository.UpdateByIdentifierAsync(identifier, appointmentEntity);
    }

    /// <summary>
    /// Marks an appointment <see cref="AppointmentStatus.Cancelled"/>, keeping the document.
    /// </summary>
    /// <returns><c>false</c> when no cancellable appointment has that identifier.</returns>
    /// <remarks>
    /// <para>
    /// A <b>soft</b> delete. This used to be <c>DeleteByIdentifierAsync</c>, which cost three things: a
    /// cancelled appointment left no record that the slot had ever been booked (so no-show and revenue
    /// reporting could not see it), <c>ReportingService.CancelledAppointments</c> could only ever be zero, and
    /// a cancellation notification named an appointment that could not be fetched.
    /// </para>
    /// <para>
    /// The <c>appointment_status</c> clause in the filter is the concurrency guard and the business rule at
    /// once: only a <c>Requested</c> or <c>Booked</c> appointment is cancellable, and putting that in the
    /// filter rather than in a preceding read means two concurrent cancellations cannot both succeed, and a
    /// completed appointment cannot be cancelled by a caller that read it before it completed. It mirrors
    /// <see cref="AppointmentEntity.Cancel"/>, which enforces the same rule in memory.
    /// </para>
    /// <para>
    /// The status is written as its integer because that is how the driver serialises the enum, and the
    /// description alongside it so the stored pair cannot disagree — the same two fields
    /// <see cref="ChangeStatusAsync"/> writes.
    /// </para>
    /// <para>
    /// <paramref name="earliestStartUtc"/> carries the customer's notice period into that SAME filter, for the
    /// same reason. Reading the appointment, comparing its start against the clock and then writing is a race a
    /// caller can win: the read and the write are separate operations, and nothing stops a request arriving in
    /// between. Expressed as a filter clause, "is there enough notice" and "cancel it" are one operation.
    /// </para>
    /// </remarks>
    /// <param name="identifier">Which appointment.</param>
    /// <param name="earliestStartUtc">
    /// When supplied, the appointment must start at or after this instant to be cancellable — the caller's
    /// notice requirement. Null means no notice requirement, which is a provider cancelling.
    /// </param>
    public async Task<bool> CancelAppointmentAsync(string identifier, DateTime? earliestStartUtc = null)
    {
        var filter = new BsonDocument
        {
            { "identifier", identifier },
            {
                "appointment_status", new BsonDocument("$in", new BsonArray
                {
                    (int)AppointmentStatus.Requested,
                    (int)AppointmentStatus.Booked,
                    // A pending reschedule proposal must not trap the appointment: either party can still
                    // cancel, mirroring AppointmentEntity.Cancel.
                    (int)AppointmentStatus.RescheduleRequested
                })
            }
        };

        if (earliestStartUtc is { } earliest)
        {
            filter.Add("start", new BsonDocument(
                "$gte", DateTime.SpecifyKind(earliest.ToUniversalTime(), DateTimeKind.Utc)));
        }

        var update = new BsonDocument("$set", new BsonDocument
        {
            { "appointment_status", (int)AppointmentStatus.Cancelled },
            {
                "appointment_description",
                EnumHelper<AppointmentStatus>.GetEnumDescription(AppointmentStatus.Cancelled)
            }
        });

        return await appointmentRepository.FindOneAndUpdateAsync(filter, update) is not null;
    }

    public async Task<AppointmentEntity> SearchAppointmentAsync(string identifier)
    {
        var filter = new BsonDocument("identifier", identifier);
        return await appointmentRepository.Find(filter);
    }

    /// <summary>
    /// Appointments for <paramref name="emailProvider"/> whose stored range overlaps
    /// [<paramref name="start"/>, <paramref name="end"/>).
    /// </summary>
    /// <remarks>
    /// Read-then-insert, not an atomic conditional write: a documented, accepted race window
    /// between this check and the caller's <see cref="BookAppointmentAsync"/> (ADR-051). Acceptable here
    /// because a provider's calendar has one writer at a time in practice, not because the race is
    /// impossible.
    /// </remarks>
    public async Task<IEnumerable<AppointmentEntity>> FindOverlappingAppointmentsAsync(
        string emailProvider, DateTime start, DateTime end)
    {
        var filter = new BsonDocument
        {
            { "email_provider", emailProvider },
            { "start", new BsonDocument("$lt", end) },
            { "end", new BsonDocument("$gt", start) },

            // A cancelled appointment does not occupy its slot. This clause became load-bearing the moment
            // cancellation stopped deleting the document: without it, cancelling would free the slot in the
            // calendar and still refuse every attempt to rebook it.
            { "appointment_status", new BsonDocument("$ne", (int)AppointmentStatus.Cancelled) }
        };
        return await appointmentRepository.FindAllAsync(filter);
    }

    /// <summary>
    /// Writes a new status onto the appointment document, and nothing else.
    /// </summary>
    /// <returns>The updated appointment, or <c>null</c> when no appointment has that identifier.</returns>
    /// <remarks>
    /// <para>
    /// A targeted <c>$set</c> through <c>FindOneAndUpdateAsync</c> (ADR-032) rather
    /// than the whole-document replacement <see cref="UpdateAppointmentAsync"/> performs. A status change
    /// touches exactly two fields, and replacing the document to change them would let a concurrent edit to
    /// <c>Start</c> or <c>End</c> be silently reverted by whichever writer read first.
    /// </para>
    /// <para>
    /// The enum is written as its integer value because that is how the driver serialises it — there is no
    /// <c>[BsonRepresentation(BsonType.String)]</c> on the property, so writing the name here would store a
    /// value the next read cannot deserialise.
    /// </para>
    /// </remarks>
    public async Task<AppointmentEntity?> ChangeStatusAsync(
        string identifier, AppointmentStatus status, string description)
    {
        return await appointmentRepository.FindOneAndUpdateAsync(
            new BsonDocument("identifier", identifier),
            new BsonDocument("$set", new BsonDocument
            {
                { "appointment_status", (int)status },
                { "appointment_description", description }
            }));
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <paramref name="expectedStatus"/> is in the FILTER, not asserted beforehand. Approving a proposal is
    /// necessarily a read-then-write from the caller's side — it has to know the proposed time to apply it — and
    /// putting the status it read into the filter is what stops the write landing on an appointment that was
    /// cancelled or completed in between. The proposal fields are unset rather than written as null, so a row
    /// with no outstanding proposal carries no keys for one.
    /// </remarks>
    public async Task<bool> ApplyRescheduleAsync(
        string identifier, AppointmentEntity rescheduled, AppointmentStatus expectedStatus)
    {
        var filter = new BsonDocument
        {
            { "identifier", identifier },
            { "appointment_status", (int)expectedStatus }
        };

        var update = new BsonDocument
        {
            {
                "$set", new BsonDocument
                {
                    { "start", DateTime.SpecifyKind(rescheduled.Start.ToUniversalTime(), DateTimeKind.Utc) },
                    { "end", DateTime.SpecifyKind(rescheduled.End.ToUniversalTime(), DateTimeKind.Utc) },
                    { "appointment_status", (int)rescheduled.AppointmentStatus },
                    { "appointment_description", rescheduled.AppointmentDescription },
                    {
                        "previous_start",
                        rescheduled.PreviousStart.HasValue
                            ? DateTime.SpecifyKind(rescheduled.PreviousStart.Value.ToUniversalTime(), DateTimeKind.Utc)
                            : BsonNull.Value
                    }
                }
            },
            { "$unset", new BsonDocument { { "proposed_start", "" }, { "proposed_by", "" } } }
        };

        return await appointmentRepository.FindOneAndUpdateAsync(filter, update) is not null;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Both rules ride in the filter: still <c>Booked</c> (so a second proposal cannot be recorded over an
    /// outstanding one, and a cancelled session cannot be proposed away) and starting after
    /// <paramref name="earliestStartUtc"/> (so a session already under way cannot be moved).
    /// </remarks>
    public async Task<bool> RecordRescheduleProposalAsync(
        string identifier, DateTime proposedStartUtc, string proposedBy, DateTime earliestStartUtc)
    {
        var filter = new BsonDocument
        {
            { "identifier", identifier },
            { "appointment_status", (int)AppointmentStatus.Booked },
            {
                "start", new BsonDocument(
                    "$gt", DateTime.SpecifyKind(earliestStartUtc.ToUniversalTime(), DateTimeKind.Utc))
            }
        };

        var update = new BsonDocument("$set", new BsonDocument
        {
            { "appointment_status", (int)AppointmentStatus.RescheduleRequested },
            {
                "appointment_description",
                EnumHelper<AppointmentStatus>.GetEnumDescription(AppointmentStatus.RescheduleRequested)
            },
            { "proposed_start", DateTime.SpecifyKind(proposedStartUtc.ToUniversalTime(), DateTimeKind.Utc) },
            { "proposed_by", proposedBy }
        });

        return await appointmentRepository.FindOneAndUpdateAsync(filter, update) is not null;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The current status is in the filter, so declining cannot resurrect an appointment that was cancelled while
    /// the proposal sat outstanding.
    /// </remarks>
    public async Task<bool> ClearRescheduleProposalAsync(string identifier)
    {
        var filter = new BsonDocument
        {
            { "identifier", identifier },
            { "appointment_status", (int)AppointmentStatus.RescheduleRequested }
        };

        var update = new BsonDocument
        {
            {
                "$set", new BsonDocument
                {
                    { "appointment_status", (int)AppointmentStatus.Booked },
                    {
                        "appointment_description",
                        EnumHelper<AppointmentStatus>.GetEnumDescription(AppointmentStatus.Booked)
                    }
                }
            },
            { "$unset", new BsonDocument { { "proposed_start", "" }, { "proposed_by", "" } } }
        };

        return await appointmentRepository.FindOneAndUpdateAsync(filter, update) is not null;
    }
}
