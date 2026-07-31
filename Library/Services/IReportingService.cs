namespace Library.Services;

public interface IReportingService
{
    Task<ProviderReport> GetProviderReportAsync(string providerEmail);
}
