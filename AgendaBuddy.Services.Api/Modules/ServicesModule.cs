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
                    return TypedResults.Ok(DataResponse<ProviderEntity>.Ok(result.Value));

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
                    return TypedResults.Ok(DataResponse<ProviderEntity>.Ok(result.Value));

                return TypedResults.NotFound();
            })
            .WithName("UpdateServicesFromProvider")
            .RequireAuthorization();
    }
}
