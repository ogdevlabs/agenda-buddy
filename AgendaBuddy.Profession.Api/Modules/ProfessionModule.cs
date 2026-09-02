using Microsoft.AspNetCore.Routing;

namespace AgendaBuddy.Profession.Api.Modules;

public class ProfessionModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var professions = app.MapGroup("api/v1/professions")
            .WithTags("ProfessionAPI")
            .WithOpenApi()
            .AddEndpointFilter<ProblemDetailsServiceEndpointFilter>();

        // ADR-025: POST /api/v1/professions was DELETED, not role-gated. There is no role to check for --
        // Identity's allow-list is exactly {Provider, Customer} with no administrative tier, so the only
        // implementable check would still let any self-registered provider write global reference data
        // read by every user. Professions are SEEDED from Library/Data/ProfessionSeedData.cs and no
        // shipped flow creates one, so nothing is lost. Pinned by ProfessionWriteRouteRemovedTest.

        professions.MapGet("",
            async Task<Results<Ok<DataResponse<List<ProfessionEntity>>>, NoContent>> (
                IMediator mediator,
                IDistributedCache cache,
                CancellationToken cancellationToken) =>
            {
                const string key = "professions";

                // A Fail result is mapped to null so CacheAside's "never cache a null"
                // rule (CacheAside.cs) keeps an empty catalogue from poisoning the cache.
                var professionCollection = await cache.GetOrCreateAsync(key, async token =>
                {
                    var result = await mediator.Send(new GetProfessionsQuery(), token);
                    return result.IsSuccess ? result.Value : null!;
                }, cancellationToken: cancellationToken);

                if (professionCollection is not null)
                    return TypedResults.Ok(DataResponse<List<ProfessionEntity>>.Ok(professionCollection));

                return TypedResults.NoContent();
            }).WithName("GetProfessionList");

        professions.MapGet("/{name}",
            async Task<Results<Ok<DataResponse<ProfessionEntity>>, NotFound>> (
                IMediator mediator,
                string name,
                IDistributedCache cache,
                CancellationToken cancellationToken) =>
            {
                var key = $"profession-{name}";

                var profession = await cache.GetOrCreateAsync(key, async token =>
                {
                    var result = await mediator.Send(new GetProfessionByNameQuery { Name = name }, token);
                    return result.IsSuccess ? result.Value : null!;
                }, cancellationToken: cancellationToken);

                if (profession is not null)
                    return TypedResults.Ok(DataResponse<ProfessionEntity>.Ok(profession));

                return TypedResults.NotFound();
            }).WithName("GetProfessionByName");

        // ── Provider-profession association ─────────────────────────────────────────────────────
        // A provider's own selection from the catalog above, not a write to the catalog itself
        // (ADR-025 still stands unchanged for /api/v1/professions and /api/v1/professions/{name}).

        professions.MapGet("/providers/{email}",
                async Task<Ok<DataResponse<List<string>>>> (
                    IMediator mediator,
                    string email,
                    CancellationToken cancellationToken) =>
                {
                    var result = await mediator.Send(new GetProfessionsFromProviderQuery { Email = email }, cancellationToken);
                    return TypedResults.Ok(DataResponse<List<string>>.Ok(result.IsSuccess ? result.Value : []));
                })
            .WithName("GetProfessionsFromProvider")
            .RequireAuthorization();

        professions.MapPut("/providers/{email}",
                async Task<Results<ValidationProblem, NotFound, Ok<DataResponse<List<string>>>>> (
                    IMediator mediator,
                    ClaimsPrincipal user,
                    string email,
                    [FromBody] List<string> professionNames,
                    CancellationToken cancellationToken) =>
                {
                    if (professionNames is null || professionNames.Count == 0)
                        return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                        {
                            { "professionNames", ["At least one profession name is required."] }
                        });

                    OwnershipGuard.AssertOwner(user, email);

                    var result = await mediator.Send(new AddProfessionsToProviderCommand
                    {
                        Email = email,
                        ProfessionNames = professionNames
                    }, cancellationToken);

                    if (result.IsSuccess)
                        return TypedResults.Ok(DataResponse<List<string>>.Ok(result.Value));

                    return TypedResults.NotFound();
                })
            .WithName("AddProfessionsToProvider")
            .RequireAuthorization();

        professions.MapDelete("/providers/{email}/{name}",
                async Task<Results<NotFound, Conflict<DataResponse<List<string>>>, Ok<DataResponse<List<string>>>>> (
                    IMediator mediator,
                    ClaimsPrincipal user,
                    string email,
                    string name,
                    CancellationToken cancellationToken) =>
                {
                    OwnershipGuard.AssertOwner(user, email);

                    var result = await mediator.Send(new RemoveProfessionFromProviderCommand
                    {
                        Email = email,
                        ProfessionName = name
                    }, cancellationToken);

                    if (result.IsSuccess)
                        return TypedResults.Ok(DataResponse<List<string>>.Ok(result.Value));

                    if (result.Errors.Any(e => e.Message == RemoveProfessionFromProviderCommandHandler.ActiveAppointmentsErrorMessage))
                        return TypedResults.Conflict(DataResponse<List<string>>.Fail(
                            [RemoveProfessionFromProviderCommandHandler.ActiveAppointmentsErrorMessage]));

                    return TypedResults.NotFound();
                })
            .WithName("RemoveProfessionFromProvider")
            .RequireAuthorization();
    }
}
