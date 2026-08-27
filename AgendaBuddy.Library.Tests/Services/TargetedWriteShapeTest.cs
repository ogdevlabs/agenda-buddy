using System.Threading.Tasks;
using AgendaBuddy.Library.Entities;
using AgendaBuddy.Library.Repositories;
using AgendaBuddy.Library.Services;
using MongoDB.Bson;
using Moq;
using Xunit;

namespace AgendaBuddy.Library.Tests.Services;

/// <summary>
/// F-014 AC-19 / requirement 20: the writes this feature adds are targeted updates, not whole-document
/// replacements.
/// </summary>
/// <remarks>
/// <para>
/// The booking path used to read the provider, append to its embedded appointment list, and call
/// <c>UpdateProviderAsync</c> — a <c>ReplaceOneAsync</c>. <b>Two concurrent bookings for one provider both
/// read, both append, and the second replacement silently discards the first appointment</b>, which then
/// exists in the `appointments` collection and not in the provider document. <c>ReportingService</c> counts
/// from the embedded copy, so the lost booking is the one that vanishes from the dashboard.
/// </para>
/// <para>
/// These tests assert the <b>shape</b> of each write rather than its effect, because the effect is identical
/// under no concurrency and the shape is the whole point. F-021 established the same discipline for its
/// counter (AC-11), and the primitive they all use arrived with it (ADR-032).
/// </para>
/// </remarks>
public class TargetedWriteShapeTest
{
    private readonly Mock<IRepository<ProviderEntity>> _providers = new();
    private readonly Mock<IRepository<AppointmentEntity>> _appointments = new();

    private BsonDocument? _filter;
    private BsonDocument? _update;

    private void CaptureProviderWrite() =>
        _providers
            .Setup(r => r.FindOneAndUpdateAsync(It.IsAny<BsonDocument>(), It.IsAny<BsonDocument>()))
            .Callback<BsonDocument, BsonDocument>((filter, update) => (_filter, _update) = (filter, update))
            .ReturnsAsync(new ProviderEntity { Email = "coach@example.com", FirstName = "C", LastName = "H" });

    [Fact]
    public async Task AppendingAnAppointment_IsASinglePush_WithNoRead()
    {
        CaptureProviderWrite();
        var service = new ProviderService(_providers.Object);

        await service.AppendAppointmentAsync("coach@example.com", new AppointmentEntity
        {
            EmailProvider = "coach@example.com",
            EmailCustomer = "ada@example.com"
        });

        Assert.Equal("coach@example.com", _filter!["email"].AsString);
        Assert.True(_update!.Contains("$push"), $"expected a $push, got: {_update}");
        Assert.True(_update["$push"].AsBsonDocument.Contains("appointments"));

        // No read at all — that is what removes the window. GetByIdAsync/Find are how the old path started.
        _providers.Verify(r => r.GetByIdAsync(It.IsAny<string>()), Times.Never);
        _providers.Verify(r => r.Find(It.IsAny<BsonDocument>()), Times.Never);
        _providers.Verify(r => r.UpdateAsync(It.IsAny<string>(), It.IsAny<ProviderEntity>()), Times.Never);
    }

    [Fact]
    public async Task ChangingAnEmbeddedAppointmentStatus_UsesThePositionalOperator()
    {
        CaptureProviderWrite();
        var service = new ProviderService(_providers.Object);

        await service.ChangeEmbeddedAppointmentStatusAsync(
            "coach@example.com", "a7f3", AppointmentStatus.Booked, "Appointment Booked");

        // Both halves of the filter matter: the provider AND the array element, or the positional operator has
        // nothing to resolve against.
        Assert.Equal("coach@example.com", _filter!["email"].AsString);
        Assert.Equal("a7f3", _filter["appointments.identifier"].AsString);

        var set = _update!["$set"].AsBsonDocument;
        Assert.Equal((int)AppointmentStatus.Booked, set["appointments.$.appointment_status"].AsInt32);
        Assert.Equal("Appointment Booked", set["appointments.$.appointment_description"].AsString);
    }

    [Fact]
    public async Task DeactivatingAProvider_SetsOneField()
    {
        CaptureProviderWrite();
        var service = new ProviderService(_providers.Object);

        await service.SetActiveAsync("coach@example.com", isActive: false);

        Assert.Equal("coach@example.com", _filter!["email"].AsString);
        Assert.False(_update!["$set"]["is_active"].AsBoolean);
        _providers.Verify(r => r.UpdateAsync(It.IsAny<string>(), It.IsAny<ProviderEntity>()), Times.Never);
    }

    [Fact]
    public async Task ChangingAnAppointmentStatus_WritesTheEnumAsAnInteger()
    {
        // The driver serialises AppointmentStatus as an int — there is no [BsonRepresentation(BsonType.String)]
        // on the property — so writing the NAME here would store a value the next read cannot deserialise, and
        // the failure would surface as a mysterious deserialisation error on an unrelated read.
        BsonDocument? filter = null, update = null;
        _appointments
            .Setup(r => r.FindOneAndUpdateAsync(It.IsAny<BsonDocument>(), It.IsAny<BsonDocument>()))
            .Callback<BsonDocument, BsonDocument>((f, u) => (filter, update) = (f, u))
            .ReturnsAsync(new AppointmentEntity { EmailProvider = "p", EmailCustomer = "c" });

        await new BookingService(_appointments.Object)
            .ChangeStatusAsync("a7f3", AppointmentStatus.Completed, "Appointment Completed");

        Assert.Equal("a7f3", filter!["identifier"].AsString);
        Assert.Equal(BsonType.Int32, update!["$set"]["appointment_status"].BsonType);
        Assert.Equal((int)AppointmentStatus.Completed, update["$set"]["appointment_status"].AsInt32);

        // A status change touches two fields. Replacing the document to change them would let a concurrent
        // edit to Start or End be reverted by whichever writer read first.
        Assert.Equal(2, update["$set"].AsBsonDocument.ElementCount);
    }
}
