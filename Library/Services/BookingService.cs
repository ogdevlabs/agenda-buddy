namespace Library.Services;

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

    public async Task<bool> CancelAppointmentAsync(string identifier)
    {
        return await appointmentRepository.DeleteByIdentifierAsync(identifier);
    }

    public async Task<AppointmentEntity> SearchAppointmentAsync(string identifier)
    {
        var filter = new BsonDocument("identifier", identifier);
        return await appointmentRepository.Find(filter);
    }

    /// <summary>
    /// Writes a new status onto the appointment document, and nothing else.
    /// </summary>
    /// <returns>The updated appointment, or <c>null</c> when no appointment has that identifier.</returns>
    /// <remarks>
    /// <para>
    /// F-014 requirement 20. A targeted <c>$set</c> through <c>FindOneAndUpdateAsync</c> (ADR-032) rather
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
}