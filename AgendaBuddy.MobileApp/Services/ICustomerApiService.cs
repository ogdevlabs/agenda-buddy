using AgendaBuddy.MobileApp.Models;

namespace AgendaBuddy.MobileApp.Services;

public interface ICustomerApiService
{
    Task<List<CustomerSummary>> GetCustomersAsync(CancellationToken ct = default);
}
