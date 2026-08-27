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
    }
}
