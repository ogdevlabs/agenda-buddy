namespace Customer.Events;

public static class EventsHelper
{
    public static async Task<string> AddCustomerEvent(IRequestCollection requestCollection, IMediator mediator,
        CustomerService customerService, CustomerEntity customerEntity)
    {
        var notificationResponse =
            await requestCollection.AddCustomerRequest(mediator, customerService, customerEntity);
        return notificationResponse;
    }
}