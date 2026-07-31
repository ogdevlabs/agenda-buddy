namespace MobileApp.Services;

public class CalendarApiService : ICalendarApiService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public CalendarApiService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }
}
