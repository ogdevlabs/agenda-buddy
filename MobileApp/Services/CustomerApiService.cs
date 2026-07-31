namespace MobileApp.Services;

public class CustomerApiService : ICustomerApiService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public CustomerApiService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }
}
