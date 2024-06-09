using Library.Entities;
using Library.Services;
using MediatR;

namespace Calendar.Requests;

public interface IRequestCollection
{
    public Task<IEnumerable<AppointmentEntity>> GetProviderAvailability(IMediator mediator,
        ProviderService providerService, string email);
}