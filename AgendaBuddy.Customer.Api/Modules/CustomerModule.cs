using Microsoft.Extensions.Caching.Distributed;

namespace AgendaBuddy.Customer.Api.Modules;

public class CustomerModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var customers = app.MapGroup("/api/v1/customers")
            .WithTags("CustomerAPI")
            .WithOpenApi()
            .AddEndpointFilter<ProblemDetailsServiceEndpointFilter>();

        // Create a Customer, verifying for duplicate record.
        customers.MapPost("/", async Task<Results<ValidationProblem, Created<DataResponse<CustomerEntity>>>> (
            IMediator mediator,
            CustomerEntity customerEntity,
            CancellationToken cancellationToken) =>
        {
            if (!MiniValidator.TryValidate(customerEntity, out var errors))
                return TypedResults.ValidationProblem(errors);

            // The duplicate-email check lives in AddCustomerCommandHandler, so
            // this route is endpoint/DI wiring only.
            var result = await mediator.Send(new AddCustomerCommand { CustomerEntity = customerEntity }, cancellationToken);

            if (result.IsSuccess)
                return TypedResults.Created($"/api/v1/customers/{customerEntity.Id}", DataResponse<CustomerEntity>.Ok(result.Value));

            return TypedResults.ValidationProblem(GenerateErrorMessage(
                "Customer Registration Error", result.Errors.Select(e => e.Message).ToArray()));
        })
        .WithName("CreateCustomer")
        .RequireAuthorization();

        customers.MapPut("/{email}",
            async Task<Results<ValidationProblem, ForbidHttpResult, NotFound, Accepted<DataResponse<CustomerEntity>>>> (
                string email,
                ClaimsPrincipal user,
                IMediator mediator,
                CustomerEntity customerEntity,
                IDistributedCache cache,
                CancellationToken cancellationToken) =>
            {
                if (!MiniValidator.TryValidate(customerEntity, out var errors))
                    return TypedResults.ValidationProblem(errors);

                // Deliberately NOT wrapped in try/catch. This is the route that demonstrates the central
                // mapping: AgendaBuddyExceptionHandler turns ForbiddenException into 403 whether or not an
                // endpoint remembered to catch it. ForbidHttpResult stays in the union above on purpose: this
                // route still returns 403, so removing it would drop 403 from the generated OpenAPI while the
                // behaviour was unchanged.
                OwnershipGuard.AssertOwner(user, email);

                var result = await mediator.Send(new UpdateCustomerCommand { Email = email, CustomerEntity = customerEntity }, cancellationToken);

                if (result.IsSuccess)
                {
                    // agenda-buddy-xrw: same gap as Provider's PUT -- the 5-minute cache-aside TTL on
                    // GET /{email} was never invalidated on write.
                    await cache.RemoveAsync($"customers-{email}", cancellationToken);
                    return TypedResults.Accepted("api/v1/customers", DataResponse<CustomerEntity>.Ok(result.Value));
                }

                return TypedResults.NotFound();
            })
            .WithName("UpdateCustomer")
            .RequireAuthorization();

        customers.MapGet("",
            async Task<Ok<DataResponse<PagedResponse<CustomerEntity>>>> (
                IMediator mediator,
                ClaimsPrincipal user,
                IDistributedCache cache,
                CancellationToken cancellationToken,
                int? page = null, int? pageSize = null) =>
            {
                // ADR-026: the Provider role, not merely a token. Authenticating this route alone was nearly
                // worthless -- POST /api/v1/auth/register is anonymous, unverified and unrate-limited, so an
                // attacker self-registers as a Customer and pages the whole customer table exactly as before.
                // Pagination bounds the response, not the extraction.
                //
                // No local try/catch: AgendaBuddyExceptionHandler maps ForbiddenException to 403 centrally.
                // Guard runs BEFORE the cache read, so a refused caller never reaches cached data.
                OwnershipGuard.AssertRole(user, "Provider");

                // ADR-023. See Provider/Program.cs for why clamping rather than rejecting.
                var pageRequest = PageRequest.Clamp(page, pageSize);

                // ⚠️ The cache key carries the page, or page 2 would serve page 1's entry. Cheap to get wrong and
                // invisible in a single-page test.
                var key = $"customers-p{pageRequest.Page}-s{pageRequest.PageSize}";
                var customerCollection = await cache.GetOrCreateAsync(key, async token =>
                {
                    var result = await mediator.Send(new GetCustomersQuery { Page = pageRequest }, token);
                    return result.IsSuccess ? result.Value : null!;
                }, cancellationToken: cancellationToken);

                // 204 is RETIRED (ADR-023): a client always gets a parseable body. CacheAside returns default! on a
                // 500 ms lock timeout, so this branch is a cache miss rather than an empty collection.
                return customerCollection is not null
                    ? TypedResults.Ok(DataResponse<PagedResponse<CustomerEntity>>.Ok(customerCollection))
                    : TypedResults.Ok(DataResponse<PagedResponse<CustomerEntity>>.Ok(PagedResponse<CustomerEntity>.From([], 0, pageRequest)));
            })
            // PII-bearing read, so no longer anonymous. Breaking change with zero reachable consumers.
            .WithName("GetAllCustomers")
            .RequireAuthorization();

        customers.MapGet("/{email}", async Task<Results<Ok<DataResponse<CustomerEntity>>, NotFound>> (
            IMediator mediator,
            string email,
            IDistributedCache cache,
            CancellationToken cancellationToken) =>
        {
            var key = $"customers-{email}";

            var customer = await cache.GetOrCreateAsync(key, async token =>
            {
                var result = await mediator.Send(new GetCustomerByEmailQuery { Email = email }, token);
                return result.IsSuccess ? result.Value : null!;
            }, cancellationToken: cancellationToken);

            if (customer is not null)
                return TypedResults.Ok(DataResponse<CustomerEntity>.Ok(customer));

            return TypedResults.NotFound();
        })
            // PII-bearing read, so no longer anonymous. Breaking change with zero reachable consumers.
            .WithName("GetCustomerByEmail")
            .RequireAuthorization();

        // ── provider subscriptions ────────────────────────────────────────────────────────────────────
        //
        // A customer can only manage their own subscriptions -- OwnershipGuard.AssertOwner on {email} the
        // same way UpdateCustomer already does. Subscribe/unsubscribe are idempotent by construction
        // ($addToSet/$pull in CustomerService), so a repeat call is a success, not a conflict.

        customers.MapPost("/{email}/subscriptions/{providerEmail}",
            async Task<Results<ForbidHttpResult, NotFound, Accepted<DataResponse<CustomerEntity>>>> (
                string email,
                string providerEmail,
                ClaimsPrincipal user,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                OwnershipGuard.AssertOwner(user, email);

                var result = await mediator.Send(
                    new SubscribeToProviderCommand { CustomerEmail = email, ProviderEmail = providerEmail }, cancellationToken);

                if (result.IsSuccess)
                    return TypedResults.Accepted("api/v1/customers", DataResponse<CustomerEntity>.Ok(result.Value));

                return TypedResults.NotFound();
            })
            .WithName("SubscribeToProvider")
            .RequireAuthorization();

        customers.MapDelete("/{email}/subscriptions/{providerEmail}",
            async Task<Results<ForbidHttpResult, NotFound, Accepted<DataResponse<CustomerEntity>>>> (
                string email,
                string providerEmail,
                ClaimsPrincipal user,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                OwnershipGuard.AssertOwner(user, email);

                var result = await mediator.Send(
                    new UnsubscribeFromProviderCommand { CustomerEmail = email, ProviderEmail = providerEmail }, cancellationToken);

                if (result.IsSuccess)
                    return TypedResults.Accepted("api/v1/customers", DataResponse<CustomerEntity>.Ok(result.Value));

                return TypedResults.NotFound();
            })
            .WithName("UnsubscribeFromProvider")
            .RequireAuthorization();

        customers.MapGet("/{email}/subscriptions",
            async Task<Results<ForbidHttpResult, NotFound, Ok<DataResponse<List<string>>>>> (
                string email,
                ClaimsPrincipal user,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                OwnershipGuard.AssertOwner(user, email);

                var result = await mediator.Send(new GetSubscribedProvidersQuery { CustomerEmail = email }, cancellationToken);

                if (result.IsSuccess)
                    return TypedResults.Ok(DataResponse<List<string>>.Ok(result.Value));

                return TypedResults.NotFound();
            })
            .WithName("GetSubscribedProviders")
            .RequireAuthorization();
    }

    private static Dictionary<string, string[]> GenerateErrorMessage(string key, string[] values) =>
        new() { { key, values } };
}
