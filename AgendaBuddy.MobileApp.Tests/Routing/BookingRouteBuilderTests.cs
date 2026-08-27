using System.Text.Json;
using AgendaBuddy.Library.Entities;
using AgendaBuddy.MobileApp.Routing;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Routing;

public class BookingRouteBuilderTests
{
    // F-015-T07 AC7: the legacy PUT booking/{id} call is replaced by F-014's dedicated status route.
    [Fact]
    public void UpdateAppointmentStatus_BuildsPostToStatusRoute()
    {
        var route = BookingRouteBuilder.UpdateAppointmentStatus("a1");

        Assert.Equal(HttpMethod.Post, route.Method);
        Assert.Equal("api/v1/booking/appointments/a1/status", route.Path);
    }

    // Payload shape unchanged: Booking's AppointmentStatusRequest(string Status) binds { "status": "…" }.
    [Fact]
    public void BuildUpdateStatusPayload_SerializesStatusAsStringProperty()
    {
        var payload = BookingRouteBuilder.BuildUpdateStatusPayload(AppointmentStatus.Completed);

        var json = JsonSerializer.Serialize(payload);

        Assert.Equal("""{"status":"Completed"}""", json);
    }

    // F-015-T07: new — F-014's session notes routes, never called by the client before this task.
    [Fact]
    public void GetNotes_BuildsGetByIdentifier()
    {
        var route = BookingRouteBuilder.GetNotes("a1");

        Assert.Equal(HttpMethod.Get, route.Method);
        Assert.Equal("api/v1/booking/appointments/a1/notes", route.Path);
    }

    [Fact]
    public void CreateNote_BuildsPostByIdentifier()
    {
        var route = BookingRouteBuilder.CreateNote("a1");

        Assert.Equal(HttpMethod.Post, route.Method);
        Assert.Equal("api/v1/booking/appointments/a1/notes", route.Path);
    }

    [Fact]
    public void BuildNotePayload_SerializesContentAsStringProperty()
    {
        var payload = BookingRouteBuilder.BuildNotePayload("Client mentioned a knee injury.");

        var json = JsonSerializer.Serialize(payload);

        Assert.Equal("""{"content":"Client mentioned a knee injury."}""", json);
    }

    // The real update route keys on the note's own id, not the appointment identifier.
    [Fact]
    public void UpdateNote_BuildsPutByNoteId()
    {
        var route = BookingRouteBuilder.UpdateNote("64f0c2f1a1b2c3d4e5f6a7b8");

        Assert.Equal(HttpMethod.Put, route.Method);
        Assert.Equal("api/v1/booking/notes/64f0c2f1a1b2c3d4e5f6a7b8", route.Path);
    }

    // F-015-T07: new — F-014's payment routes, never called by the client before this task.
    [Fact]
    public void GetPayment_BuildsGetByIdentifier()
    {
        var route = BookingRouteBuilder.GetPayment("a1");

        Assert.Equal(HttpMethod.Get, route.Method);
        Assert.Equal("api/v1/booking/appointments/a1/payment", route.Path);
    }

    [Fact]
    public void CreatePayment_BuildsPostByIdentifier()
    {
        var route = BookingRouteBuilder.CreatePayment("a1");

        Assert.Equal(HttpMethod.Post, route.Method);
        Assert.Equal("api/v1/booking/appointments/a1/payment", route.Path);
    }

    [Fact]
    public void BuildPaymentPayload_SerializesAmountAndCurrency()
    {
        var payload = BookingRouteBuilder.BuildPaymentPayload(50m, "usd");

        var json = JsonSerializer.Serialize(payload);

        Assert.Equal("""{"amount":50,"currency":"usd"}""", json);
    }
}
