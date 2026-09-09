using AgendaBuddy.Library.Entities;

namespace AgendaBuddy.MobileApp.Resources.Strings;

public static class RuntimeText
{
    public static string Role(string role) => role.ToLowerInvariant() switch
    {
        "provider" => AppResources.GetString("Role_Provider"),
        "customer" => AppResources.GetString("Role_Customer"),
        _ => string.Empty
    };

    public static string AppointmentStatus(AppointmentStatus status) => status switch
    {
        AgendaBuddy.Library.Entities.AppointmentStatus.Requested => AppResources.GetString("AppointmentStatus_Requested"),
        AgendaBuddy.Library.Entities.AppointmentStatus.Booked => AppResources.GetString("AppointmentStatus_Booked"),
        AgendaBuddy.Library.Entities.AppointmentStatus.Completed => AppResources.GetString("AppointmentStatus_Completed"),
        AgendaBuddy.Library.Entities.AppointmentStatus.Confirmed => AppResources.GetString("AppointmentStatus_Confirmed"),
        AgendaBuddy.Library.Entities.AppointmentStatus.Cancelled => AppResources.GetString("AppointmentStatus_Cancelled"),
        AgendaBuddy.Library.Entities.AppointmentStatus.RescheduleRequested => AppResources.GetString("AppointmentStatus_RescheduleRequested"),
        _ => AppResources.GetString("AppointmentStatus_Unknown")
    };

    public static string FeeType(FeeType feeType) => feeType switch
    {
        AgendaBuddy.Library.Entities.FeeType.Fixed => AppResources.GetString("FeeType_Fixed"),
        AgendaBuddy.Library.Entities.FeeType.Hourly => AppResources.GetString("FeeType_Hourly"),
        AgendaBuddy.Library.Entities.FeeType.Subscription => AppResources.GetString("FeeType_Subscription"),
        _ => AppResources.GetString("FeeType_Unknown")
    };

    public static string Duration(int minutes) =>
        AppResources.Format(minutes == 1 ? "Duration_OneMinute" : "Duration_ManyMinutes", minutes);

    public static string Currency(decimal amount) => amount.ToString("C", AppResources.CurrentCulture);

    public static string RelativeTime(DateTime value, DateTime now)
    {
        var difference = now - value;
        if (difference < TimeSpan.Zero || difference.TotalMinutes < 1)
            return AppResources.GetString("RelativeTime_Now");
        if (difference.TotalMinutes < 60)
            return AppResources.Format("RelativeTime_MinutesAgo", (int)difference.TotalMinutes);
        if (difference.TotalHours < 24)
            return AppResources.Format("RelativeTime_HoursAgo", (int)difference.TotalHours);
        return AppResources.Format("RelativeTime_DaysAgo", (int)difference.TotalDays);
    }

    public static string GatewayService(string service) => service.ToLowerInvariant() switch
    {
        "booking" => AppResources.GetString("GatewayService_Booking"),
        "calendar" => AppResources.GetString("GatewayService_Calendar"),
        "customer" => AppResources.GetString("GatewayService_Customers"),
        "provider" => AppResources.GetString("GatewayService_Providers"),
        "services" => AppResources.GetString("GatewayService_Services"),
        "profession" => AppResources.GetString("GatewayService_Professions"),
        "identity" => AppResources.GetString("GatewayService_Account"),
        _ => string.Empty
    };
}