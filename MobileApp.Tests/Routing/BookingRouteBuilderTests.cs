using System.Text.Json;
using Library.Entities;
using MobileApp.Routing;
using Xunit;

namespace MobileApp.Tests.Routing;

public class BookingRouteBuilderTests
{
    // Pins BookingApiService.GetTodayAppointmentsAsync's current (pre-F-015-T07) route:
    // $"booking?date={DateTime.UtcNow:yyyy-MM-dd}" — no api/v1 prefix, singular resource name.
    [Fact]
    public void TodayAppointments_BuildsGetWithDateQueryParam()
    {
        var route = BookingRouteBuilder.TodayAppointments(new DateOnly(2026, 7, 31));

        Assert.Equal(HttpMethod.Get, route.Method);
        Assert.Equal("booking?date=2026-07-31", route.Path);
    }

    // Pins BookingApiService.GetAppointmentAsync's current route: $"booking/{id}".
    [Fact]
    public void Appointment_BuildsGetById()
    {
        var route = BookingRouteBuilder.Appointment("a1");

        Assert.Equal(HttpMethod.Get, route.Method);
        Assert.Equal("booking/a1", route.Path);
    }

    // Pins BookingApiService.UpdateStatusAsync's current route: PUT $"booking/{id}".
    [Fact]
    public void UpdateAppointmentStatus_BuildsPutById()
    {
        var route = BookingRouteBuilder.UpdateAppointmentStatus("a1");

        Assert.Equal(HttpMethod.Put, route.Method);
        Assert.Equal("booking/a1", route.Path);
    }

    // Pins BookingApiService.UpdateStatusAsync's current body shape: { "status": "<enum name>" }.
    [Fact]
    public void BuildUpdateStatusPayload_SerializesStatusAsStringProperty()
    {
        var payload = BookingRouteBuilder.BuildUpdateStatusPayload(AppointmentStatus.Confirmed);

        var json = JsonSerializer.Serialize(payload);

        Assert.Equal("""{"status":"Confirmed"}""", json);
    }
}
