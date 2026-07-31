namespace MobileApp.Services;

public class MessagingApiService : IMessagingApiService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public MessagingApiService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }
}
