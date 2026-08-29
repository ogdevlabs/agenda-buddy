namespace AgendaBuddy.Provider.Api.Modules;

public class ProviderModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var providers = app.MapGroup("/api/v1/providers")
            .WithTags("ProviderAPI")
            .WithOpenApi()
            .AddEndpointFilter<ProblemDetailsServiceEndpointFilter>();

        // Create a Provider, verifying for duplicate record
        // create a Topic for the provider
        providers.MapPost("/", async Task<Results<ValidationProblem, Created<DataResponse<ProviderEntity>>>> (
                IMediator mediator,
                ClaimsPrincipal user,
                ProviderEntity providerEntity,
                IDistributedCache cache,
                CancellationToken cancellationToken) =>
            {
                if (!MiniValidator.TryValidate(providerEntity, out var errors))
                    return TypedResults.ValidationProblem(errors);

                // BOTH arms are required. A role check alone still lets one Provider create a
                // record under another provider's email, which is account takeover by registration. An ownership
                // check alone would let a Customer create provider records for themselves.
                //
                // This is one of only two AssertRole call sites in the solution -- per 13-security.md:137, the
                // `role` claim otherwise authorizes nothing at all.
                //
                // No local try/catch: AgendaBuddyExceptionHandler maps ForbiddenException to 403 centrally.
                OwnershipGuard.AssertRole(user, "Provider");
                OwnershipGuard.AssertOwner(user, providerEntity.Email);

                // Dispatched through mediator.Send with the request's CancellationToken. The duplicate-name
                // check and Kafka topic creation both live in AddProviderCommandHandler, so this route is
                // endpoint/DI wiring only.
                var result = await mediator.Send(new AddProviderCommand { ProviderEntity = providerEntity }, cancellationToken);

                if (result.IsSuccess)
                {
                    // GetAllProviders below caches by page/pageSize (key "providers-p{page}-s{size}"), and
                    // a new provider changes that list's content. The mobile client only ever requests
                    // page=1/size=25 (ProviderRouteBuilder.Providers' own defaults, which PageRequest.Clamp
                    // passes through unchanged) -- the one key actually served, not a general invalidate-all.
                    // A newly-registered provider was invisible to the directory for up to the 5-minute TTL
                    // without this.
                    // Both variants of the page the mobile client actually requests -- the filtered one is
                    // what the directory reads, the unfiltered one is the route's default.
                    await cache.RemoveAsync("providers-p1-s25-bTrue", cancellationToken);
                    await cache.RemoveAsync("providers-p1-s25-bFalse", cancellationToken);
                    return TypedResults.Created($"/api/v1/providers/{providerEntity.Id}", DataResponse<ProviderEntity>.Ok(result.Value));
                }

                return TypedResults.ValidationProblem(GenerateErrorMessage(
                    "Provider Registration Error", result.Errors.Select(e => e.Message).ToArray()));
            })
            .WithName("CreateProvider")
            .RequireAuthorization();

        // Get provider list
        providers.MapGet("", async Task<Ok<DataResponse<PagedResponse<ProviderSummary>>>> (
            IMediator mediator,
            IDistributedCache cache,
            CancellationToken cancellationToken,
            int? page = null, int? pageSize = null, bool bookableOnly = false) =>
        {
            // ADR-023. Clamped, never rejected: a 400 would tell an attacker the exact boundary and
            // leave an honest client no way to discover the cap. MaxPageSize is a SECURITY control -- an uncapped
            // page size would restore a full-dataset dump.
            var pageRequest = PageRequest.Clamp(page, pageSize);

            // ⚠️ The cache key carries the page, or page 2 would serve page 1's entry. Cheap to get wrong and
            // invisible in a single-page test.
            // bookableOnly is part of the key: the filtered and unfiltered pages are different result
            // sets, and sharing one entry would serve whichever was cached first to both callers.
            var key = $"providers-p{pageRequest.Page}-s{pageRequest.PageSize}-b{bookableOnly}";
            var providerCollection = await cache.GetOrCreateAsync(key, async token =>
            {
                var result = await mediator.Send(
                    new GetProvidersQuery { Page = pageRequest, BookableOnly = bookableOnly }, token);
                return result.IsSuccess ? result.Value : null!;
            }, cancellationToken: cancellationToken);

            if (providerCollection is null)
            {
                // 204 is RETIRED (ADR-023): a client always gets a parseable body. CacheAside returns default! on a
                // 500 ms lock timeout, so this branch is a cache miss rather than an empty collection.
                return TypedResults.Ok(DataResponse<PagedResponse<ProviderSummary>>.Ok(
                    PagedResponse<ProviderSummary>.From([], 0, pageRequest)));
            }

            // ProviderEntity embeds AppointmentEntities (each carrying email_customer) and
            // SubscribedCustomerCollection, so authentication alone does not fix this: an authenticated
            // CUSTOMER browsing for a coach would still receive every provider's appointment book and client
            // roster.
            //
            // ⚠️ THE LIST IS HOMOGENEOUS -- every element is a ProviderSummary, including the caller's own record.
            // An owner loses nothing: GET /api/v1/providers/{email} returns their full record, and that route
            // DOES apply the ownership branch. Deviation recorded in api-contracts.md.
            return TypedResults.Ok(DataResponse<PagedResponse<ProviderSummary>>.Ok(PagedResponse<ProviderSummary>.From(
                providerCollection.Items.Select(ProviderSummary.From).ToList(),
                providerCollection.TotalCount,
                pageRequest)));
        })
            // PII-bearing read, so no longer anonymous. Breaking change with zero reachable consumers.
            .WithName("GetAllProviders")
            .RequireAuthorization();

        // Get provider by Email
        providers.MapGet("/{email}", async Task<Results<Ok<DataResponse<ProviderEntity>>, Ok<DataResponse<ProviderSummary>>, NotFound>> (
            IMediator mediator,
            ClaimsPrincipal user,
            string email,
            IDistributedCache cache,
            CancellationToken cancellationToken) =>
        {
            var key = $"providers-{email}";

            var providerEntity = await cache.GetOrCreateAsync(key, async token =>
            {
                var result = await mediator.Send(new GetProviderByEmailQuery { Email = email }, token);
                return result.IsSuccess ? result.Value : null!;
            }, cancellationToken: cancellationToken);

            if (providerEntity is null)
                return TypedResults.NotFound();

            // Two shapes, selected by ownership. Deliberately NOT 403 for a provider you do not own -- reading
            // another provider's SUMMARY is a supported discovery flow. Only the embedded data is withheld.
            //
            // ⚠️ AssertOwner's null-claim fall-through used to land on the OWNER side, so a token carrying no
            // `sub` would have received the unprojected entity. Pinned by ProviderProjectionTest.T001_*.
            // IsOwner rather than catching AssertOwner's ForbiddenException: "not the owner" selects a narrower
            // shape here, it is not a failure, and exception-driven control flow on a read path is both slower and
            // misleading. Both share one implementation, so the null-claim rule cannot drift between them.
            return OwnershipGuard.IsOwner(user, providerEntity.Email)
                ? TypedResults.Ok(DataResponse<ProviderEntity>.Ok(providerEntity))
                : TypedResults.Ok(DataResponse<ProviderSummary>.Ok(ProviderSummary.From(providerEntity)));
        })
            // PII-bearing read, so no longer anonymous. Breaking change with zero reachable consumers.
            .WithName("GetProviderByEmail")
            .RequireAuthorization();

        // Update a provider, using email for search of the record
        providers.MapPut("/{email}", async Task<Results<ValidationProblem, ForbidHttpResult, NotFound, Accepted<DataResponse<ProviderEntity>>>> (
            string email,
            ClaimsPrincipal user,
            IMediator mediator,
            ProviderEntity providerEntity,
            IDistributedCache cache,
            CancellationToken cancellationToken) =>
        {
            if (!MiniValidator.TryValidate(providerEntity, out var errors))
                return TypedResults.ValidationProblem(errors);

            try { OwnershipGuard.AssertOwner(user, email); }
            catch (ForbiddenException) { return TypedResults.Forbid(); }

            var result = await mediator.Send(new UpdateProviderCommand { Email = email, ProviderEntity = providerEntity }, cancellationToken);

            if (result.IsSuccess)
            {
                // agenda-buddy-xrw: the 5-minute cache-aside TTL on GET /{email} was never invalidated
                // on write, so a provider saving their own profile (AccountViewModel.SaveProfileAsync)
                // couldn't see the change reflected back for up to 5 minutes.
                await cache.RemoveAsync($"providers-{email}", cancellationToken);
                return TypedResults.Accepted("api/v1/providers", DataResponse<ProviderEntity>.Ok(result.Value));
            }

            return TypedResults.NotFound();
        })
        .WithName("UpdateProvider")
        .RequireAuthorization();

        // ── Reporting and deactivation ────────────────────────────────────────────────────────────────

        // A provider's own metrics. {email} is in the path for symmetry with the other provider routes, NOT as a
        // selector — it must equal the caller's own claim, so there is nothing to enumerate.
        //
        // ⚠️ The report carries NO revenue figure, deliberately. The old formula was completed
        // appointments × the whole service catalogue's fees, and it cannot be corrected by arithmetic because an
        // appointment does not record which service it was booked for. `revenueAvailable: false` plus a reason,
        // rather than a plausible number that would be believed.
        //
        // Deliberately NOT wrapped in DataResponse<T>, unlike every other route in this file. This route calls
        // IReportingService directly rather than going through MediatR/Result<T>, and
        // ReportAndDeactivationTest deserialises the body at the root (ReadFromJsonAsync<ProviderReport>, and
        // a root-level "revenueAvailable"/"revenueUnavailableReason"). Wrapping it would be a real behaviour
        // change. See AgendaBuddy.Provider.Domain.Responses.DataResponse's own remarks.
        providers.MapGet("/{email}/report",
                async Task<Results<Ok<ProviderReport>, ForbidHttpResult, NotFound>> (
                    string email, ClaimsPrincipal user, IReportingService reporting) =>
                {
                    try
                    {
                        OwnershipGuard.AssertRole(user, "Provider");
                        OwnershipGuard.AssertOwner(user, email);

                        return TypedResults.Ok(await reporting.GetProviderReportAsync(email));
                    }
                    catch (ForbiddenException) { return TypedResults.Forbid(); }
                    // Safe: the caller has already proven the path email is their own claim, so this can only mean
                    // their own provider record is missing.
                    catch (KeyNotFoundException) { return TypedResults.NotFound(); }
                })
            .WithName("GetProviderReport")
            .RequireAuthorization();

        // A provider deactivates THEMSELVES. Role plus ownership, and no administrative bypass —
        // because there is no administrative role in this product (Identity's allow-list is exactly
        // {Provider, Customer}), so there is nobody else who could legitimately call this. An unguarded
        // version would let anyone take a business offline.
        providers.MapPost("/{email}/deactivate",
                async Task<Results<Accepted<DataResponse<ProviderEntity>>, ForbidHttpResult, NotFound>> (
                    string email,
                    ClaimsPrincipal user,
                    IMediator mediator,
                    IProviderService providerService,
                    IDistributedCache cache,
                    CancellationToken cancellationToken) =>
                {
                    try
                    {
                        OwnershipGuard.AssertRole(user, "Provider");
                        OwnershipGuard.AssertOwner(user, email);
                    }
                    catch (ForbiddenException) { return TypedResults.Forbid(); }

                    var existing = await providerService.FindProvidersAsync(
                        SupportTools<ProviderEntity>.FilterByEmail(email));
                    if (existing is null) return TypedResults.NotFound();

                    var result = await mediator.Send(new DeactivateProviderCommand { ProviderEntity = existing }, cancellationToken);

                    if (!result.IsSuccess)
                        return TypedResults.NotFound();

                    await cache.RemoveAsync($"providers-{email}", cancellationToken);
                    return TypedResults.Accepted($"/api/v1/providers/{email}", DataResponse<ProviderEntity>.Ok(result.Value));
                })
            .WithName("DeactivateProvider")
            .RequireAuthorization();
    }

    private static Dictionary<string, string[]> GenerateErrorMessage(string key, string[] values) =>
        new() { { key, values } };
}
