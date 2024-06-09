using EventAndCommands.Queries.Calendar;
using Library.Entities;
using Library.Services;
using MediatR;

namespace Calendar.Requests;

public class RequestCollection : IRequestCollection
{
    public async Task<IEnumerable<AppointmentEntity>> GetProviderAvailability(IMediator mediator,
        ProviderService providerService, string email)
    {
        var result =
            await new CheckAvailabilityQueryHandler(mediator, providerService, email).Handle(
                new CheckAvailabilityQuery(), new CancellationToken());
        return result;
    }
}