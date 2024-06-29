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

    public static async Task<string> UpdateCustomerEvent(string email, IRequestCollection requestCollection,
        IMediator mediator,
        CustomerService customerService, CustomerEntity customerEntity)
    {
        var notificationResponse =
            await requestCollection.UpdateCustomerRequest(email, mediator, customerService, customerEntity);
        return notificationResponse;
    }

    public static async Task<IEnumerable<CustomerEntity>> GetCustomersEvent(IRequestCollection requestCollection,
        IMediator mediator,
        CustomerService customerService)
    {
        var notificationResponse =
            await requestCollection.GetCustomersRequest(mediator, customerService);
        return notificationResponse;
    }

    public static async Task<CustomerEntity> GetCustomerByEmailEvent(IRequestCollection requestCollection,
        IMediator mediator, CustomerService customerService, string email)
    {
        var notificationResponse = await requestCollection.GetCustomerByEmail(mediator, customerService, email);
        return notificationResponse;
    }
}