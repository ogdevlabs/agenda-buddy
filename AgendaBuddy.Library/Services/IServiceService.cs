namespace AgendaBuddy.Library.Services;

public interface IServiceService
{
    public Task<List<ServiceEntity>> AddServicesAsync(List<ServiceEntity> serviceEntities);
    public Task<List<ServiceEntity>> UpdateServicesAsync(List<ServiceEntity> serviceEntities);
}
