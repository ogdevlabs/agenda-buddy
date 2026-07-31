namespace MobileApp.Services;

public class NotificationApiService : INotificationApiService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public NotificationApiService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }
}
