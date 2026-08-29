using AgendaBuddy.Library.Tools;
using Microsoft.Extensions.Caching.Distributed;

namespace AgendaBuddy.Services.Api.Modules;

public class ServicesModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var services = app.MapGroup("api/v1/services")
            .WithTags("ServiceAPI")
            .WithOpenApi()
            .AddEndpointFilter<ProblemDetailsServiceEndpointFilter>();

        services.MapGet("/{email}",
            async Task<Results<Ok<DataResponse<List<ServiceEntity>>>, NotFound>> (
                IMediator mediator,
                string email,
                IDistributedCache cache,
                CancellationToken cancellationToken) =>
            {
                var key = $"services-{email}";

                // Dispatched through mediator.Send with the request's CancellationToken. A missing provider is
                // a successful empty read (see the handler's own remarks), so this Fail-to-null mapping and the
                // NotFound branch below are unreachable in practice -- preserved anyway, matching every other
                // migrated service's shape.
                var serviceEntities = await cache.GetOrCreateAsync(key, async token =>
                {
                    var result = await mediator.Send(new GetServicesFromProviderQuery { Email = email }, token);
                    return result.IsSuccess ? result.Value : null!;
                }, cancellationToken: cancellationToken);

                if (serviceEntities is not null)
                    return TypedResults.Ok(DataResponse<List<ServiceEntity>>.Ok(serviceEntities));

                return TypedResults.NotFound();
            })
            // PII-bearing read, so no longer anonymous. Breaking change with zero reachable consumers.
            .WithName("GetServicesFromProvider")
            .RequireAuthorization();

        services.MapPut("/{email}",
            async Task<Results<ValidationProblem, NotFound, Ok<DataResponse<ProviderEntity>>>> (
                IMediator mediator,
                ClaimsPrincipal user,
                [FromBody] List<ServiceEntity> serviceEntities,
                string email,
                IDistributedCache cache,
                CancellationToken cancellationToken) =>
            {
                if (!MiniValidator.TryValidate(serviceEntities, out var errors))
                    return TypedResults.ValidationProblem(errors);

                // No local try/catch: AgendaBuddyExceptionHandler maps ForbiddenException to 403 centrally.
                OwnershipGuard.AssertOwner(user, email);

                var result = await mediator.Send(new AddServicesToProviderCommand
                {
                    Email = email,
                    ServiceEntities = serviceEntities
                }, cancellationToken);

                if (result.IsSuccess)
                {
                    // agenda-buddy-xrw: cache-aside reads were never invalidated on write, so the GET
                    // route's 5-minute TTL kept serving the pre-write list. This is the one instance of
                    // that gap actually fixed so far -- the other cached services still have it.
                    await cache.RemoveAsync($"services-{email}", cancellationToken);
                    return TypedResults.Ok(DataResponse<ProviderEntity>.Ok(result.Value));
                }

                return TypedResults.NotFound();
            })
            .WithName("AddServicesToProvider")
            .RequireAuthorization();

        services.MapPatch("/{email}",
            async Task<Results<ValidationProblem, NotFound, Ok<DataResponse<ProviderEntity>>>> (
                IMediator mediator,
                ClaimsPrincipal user,
                [FromBody] List<ServiceEntity> serviceEntities,
                string email,
                IDistributedCache cache,
                CancellationToken cancellationToken) =>
            {
                if (!MiniValidator.TryValidate(serviceEntities, out var errors))
                    return TypedResults.ValidationProblem(errors);

                // No local try/catch: AgendaBuddyExceptionHandler maps ForbiddenException to 403 centrally.
                OwnershipGuard.AssertOwner(user, email);

                var result = await mediator.Send(new UpdateServicesFromProviderCommand
                {
                    Email = email,
                    ServiceEntities = serviceEntities
                }, cancellationToken);

                if (result.IsSuccess)
                {
                    await cache.RemoveAsync($"services-{email}", cancellationToken);
                    return TypedResults.Ok(DataResponse<ProviderEntity>.Ok(result.Value));
                }

                return TypedResults.NotFound();
            })
            .WithName("UpdateServicesFromProvider")
            .RequireAuthorization();

        // {name} is matched exactly against ServiceEntity.Name, same key AddServicesToProvider/
        // UpdateServicesFromProvider already use — there is no id-based lookup for a service anywhere
        // in this route group (agenda-buddy-do5: Services.Api doesn't register ObjectIdJsonConverter, so
        // a service's id is unusable on the wire regardless).
        services.MapDelete("/{email}/{name}",
            async Task<Results<NotFound, Ok<DataResponse<ProviderEntity>>>> (
                IMediator mediator,
                ClaimsPrincipal user,
                string email,
                string name,
                IDistributedCache cache,
                CancellationToken cancellationToken) =>
            {
                OwnershipGuard.AssertOwner(user, email);

                var result = await mediator.Send(new RemoveServiceFromProviderCommand
                {
                    Email = email,
                    ServiceName = name
                }, cancellationToken);

                if (result.IsSuccess)
                {
                    await cache.RemoveAsync($"services-{email}", cancellationToken);
                    return TypedResults.Ok(DataResponse<ProviderEntity>.Ok(result.Value));
                }

                return TypedResults.NotFound();
            })
            .WithName("RemoveServiceFromProvider")
            .RequireAuthorization();
    }
}
