using MobileApp.Models;

namespace MobileApp.Services;

public interface ICustomerApiService
{
    Task<List<CustomerSummary>> GetCustomersAsync(CancellationToken ct = default);
}
