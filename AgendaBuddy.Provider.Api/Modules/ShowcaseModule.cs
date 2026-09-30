using AgendaBuddy.Library.Showcase;
using AgendaBuddy.Provider.Api.Showcase;
using AgendaBuddy.Provider.Domain.Showcase;

namespace AgendaBuddy.Provider.Api.Modules;

/// <summary>
/// The provider showcase. No route carries an email address: the owner routes act on the caller's own <c>sub</c>
/// and the viewing routes are keyed on the opaque provider reference or the public code.
/// </summary>
public class ShowcaseModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var showcase = app.MapGroup("/api/v1/showcase")
            .WithTags("ShowcaseAPI")
            .WithOpenApi()
            .AddEndpointFilter<ProblemDetailsServiceEndpointFilter>()
            .RequireAuthorization();

        showcase.MapGet("/me", async (IMediator mediator, ClaimsPrincipal user, HttpContext http, CancellationToken ct) =>
                Owner(http, await mediator.Send(new GetMyShowcaseQuery { Email = ShowcaseHttp.ProviderEmail(user) }, ct)))
            .WithName("GetMyShowcase")
            .Produces<DataResponse<ShowcaseOwnerView>>();

        showcase.MapPut("/me/text", async (IMediator mediator, ClaimsPrincipal user, HttpContext http,
                ShowcaseTextRequest body, CancellationToken ct) =>
                Owner(http, await mediator.Send(new SetShowcaseTextCommand
                {
                    Email = ShowcaseHttp.ProviderEmail(user),
                    Tagline = body.Tagline,
                    About = body.About
                }, ct)))
            .WithName("SetShowcaseText")
            .Produces<DataResponse<ShowcaseOwnerView>>();

        showcase.MapPut("/me/photo", (IMediator mediator, ClaimsPrincipal user, HttpContext http,
                ShowcaseImageRequest body, CancellationToken ct) =>
                SetImage(mediator, user, http, ShowcaseImageSlot.Photo, body, ct))
            .WithName("SetShowcasePhoto")
            .Produces<DataResponse<ShowcaseOwnerView>>();

        showcase.MapPut("/me/logo", (IMediator mediator, ClaimsPrincipal user, HttpContext http,
                ShowcaseImageRequest body, CancellationToken ct) =>
                SetImage(mediator, user, http, ShowcaseImageSlot.Logo, body, ct))
            .WithName("SetShowcaseLogo")
            .Produces<DataResponse<ShowcaseOwnerView>>();

        showcase.MapPost("/me/portfolio", async (IMediator mediator, ClaimsPrincipal user, HttpContext http,
                AddPortfolioItemRequest body, CancellationToken ct) =>
            {
                var result = await mediator.Send(new AddPortfolioItemCommand
                {
                    Email = ShowcaseHttp.ProviderEmail(user),
                    Hash = body.Hash ?? string.Empty,
                    Caption = body.Caption,
                    ServiceId = body.ServiceId
                }, ct);
                if (result.IsFailed)
                    return ShowcaseHttp.Failure(http, result.Errors);

                var envelope = DataResponse<PortfolioItemView>.Ok(result.Value.Item);
                return result.Value.Created
                    ? TypedResults.Created($"/api/v1/showcase/me/portfolio/{result.Value.Item.Hash}", envelope)
                    : TypedResults.Ok(envelope);
            })
            .WithName("AddPortfolioItem")
            .Produces<DataResponse<PortfolioItemView>>(StatusCodes.Status201Created)
            .Produces<DataResponse<PortfolioItemView>>();

        // Registered before "/me/portfolio/{hash}" so "order" is never read as a hash.
        showcase.MapPut("/me/portfolio/order", async (IMediator mediator, ClaimsPrincipal user, HttpContext http,
                ReorderPortfolioRequest body, CancellationToken ct) =>
                Owner(http, await mediator.Send(new ReorderPortfolioCommand
                {
                    Email = ShowcaseHttp.ProviderEmail(user),
                    Hashes = body.Hashes ?? []
                }, ct)))
            .WithName("ReorderPortfolio")
            .Produces<DataResponse<ShowcaseOwnerView>>();

        showcase.MapPatch("/me/portfolio/{hash}", async (IMediator mediator, ClaimsPrincipal user, HttpContext http,
                string hash, UpdatePortfolioItemRequest body, CancellationToken ct) =>
            {
                var result = await mediator.Send(new UpdatePortfolioItemCommand
                {
                    Email = ShowcaseHttp.ProviderEmail(user),
                    Hash = hash,
                    Caption = body.Caption,
                    ServiceId = body.ServiceId
                }, ct);
                return result.IsFailed
                    ? ShowcaseHttp.Failure(http, result.Errors)
                    : TypedResults.Ok(DataResponse<PortfolioItemView>.Ok(result.Value));
            })
            .WithName("UpdatePortfolioItem")
            .Produces<DataResponse<PortfolioItemView>>();

        showcase.MapDelete("/me/portfolio/{hash}", async (IMediator mediator, ClaimsPrincipal user,
                string hash, CancellationToken ct) =>
            {
                await mediator.Send(new RemovePortfolioItemCommand { Email = ShowcaseHttp.ProviderEmail(user), Hash = hash }, ct);
                return TypedResults.NoContent();
            })
            .WithName("RemovePortfolioItem");

        showcase.MapPost("/me/code", async (IMediator mediator, ClaimsPrincipal user, HttpContext http, CancellationToken ct) =>
            {
                var result = await mediator.Send(new CreatePublicCodeCommand { Email = ShowcaseHttp.ProviderEmail(user) }, ct);
                return result.IsFailed
                    ? ShowcaseHttp.Failure(http, result.Errors)
                    : TypedResults.Ok(DataResponse<PublicCodeView>.Ok(result.Value));
            })
            .WithName("CreatePublicCode")
            .Produces<DataResponse<PublicCodeView>>();

        showcase.MapGet("/by-code/{code}", async (IMediator mediator, ClaimsPrincipal user, HttpContext http,
                string code, string? source, CancellationToken ct) =>
                View(http, await mediator.Send(new GetShowcaseQuery
                {
                    CallerEmail = ShowcaseHttp.CallerEmail(user),
                    Code = code,
                    Source = source
                }, ct)))
            .WithName("GetShowcaseByCode")
            .RequireRateLimiting(ShowcaseRateLimiting.ByCodePolicy)
            .Produces<DataResponse<ShowcaseView>>();

        showcase.MapGet("/hidden", async (IMediator mediator, ClaimsPrincipal user, CancellationToken ct) =>
            {
                OwnershipGuard.AssertRole(user, "Customer");
                var result = await mediator.Send(new GetHiddenProvidersQuery { CustomerEmail = ShowcaseHttp.CallerEmail(user) }, ct);
                return TypedResults.Ok(DataResponse<IReadOnlyList<HiddenProvider>>.Ok(result.ValueOrDefault ?? []));
            })
            .WithName("GetHiddenProviders");

        showcase.MapPost("/lookup", async (IMediator mediator, ClaimsPrincipal user, HttpContext http,
                ShowcaseLookupRequest body, CancellationToken ct) =>
            {
                var result = await mediator.Send(new LookupShowcaseAvatarsQuery
                {
                    CallerEmail = ShowcaseHttp.CallerEmail(user),
                    CallerIsProvider = user.IsInRole("Provider"),
                    Emails = body.Emails ?? []
                }, ct);
                return result.IsFailed
                    ? ShowcaseHttp.Failure(http, result.Errors)
                    : TypedResults.Ok(DataResponse<IReadOnlyList<ShowcaseAvatarLookup>>.Ok(result.Value));
            })
            .WithName("LookupShowcaseAvatars")
            .Produces<DataResponse<IReadOnlyList<ShowcaseAvatarLookup>>>();

        showcase.MapGet("/{providerRef}", async (IMediator mediator, ClaimsPrincipal user, HttpContext http,
                string providerRef, string? source, CancellationToken ct) =>
                View(http, await mediator.Send(new GetShowcaseQuery
                {
                    CallerEmail = ShowcaseHttp.CallerEmail(user),
                    ProviderRef = providerRef,
                    Source = source
                }, ct)))
            .WithName("GetShowcase")
            .Produces<DataResponse<ShowcaseView>>();

        showcase.MapPost("/{providerRef}/report", async (IMediator mediator, ClaimsPrincipal user, HttpContext http,
                string providerRef, ShowcaseReportRequest body, CancellationToken ct) =>
            {
                var result = await mediator.Send(new ReportShowcaseCommand
                {
                    ReporterEmail = ShowcaseHttp.CallerEmail(user),
                    ProviderRef = providerRef,
                    Reason = body.Reason ?? string.Empty,
                    Detail = body.Detail,
                    PortfolioHash = body.PortfolioHash
                }, ct);
                return result.IsFailed ? ShowcaseHttp.Failure(http, result.Errors) : TypedResults.Accepted((string?)null);
            })
            .WithName("ReportShowcase");

        showcase.MapPut("/{providerRef}/hide", (IMediator mediator, ClaimsPrincipal user, HttpContext http,
                string providerRef, CancellationToken ct) => SetHidden(mediator, user, http, providerRef, true, ct))
            .WithName("HideProvider");

        showcase.MapDelete("/{providerRef}/hide", (IMediator mediator, ClaimsPrincipal user, HttpContext http,
                string providerRef, CancellationToken ct) => SetHidden(mediator, user, http, providerRef, false, ct))
            .WithName("UnhideProvider");
    }

    private static IResult Owner(HttpContext http, FluentResults.Result<ShowcaseOwnerView> result) =>
        result.IsFailed
            ? ShowcaseHttp.Failure(http, result.Errors)
            : TypedResults.Ok(DataResponse<ShowcaseOwnerView>.Ok(result.Value));

    private static IResult View(HttpContext http, FluentResults.Result<ShowcaseView> result) =>
        result.IsFailed
            ? ShowcaseHttp.Failure(http, result.Errors)
            : TypedResults.Ok(DataResponse<ShowcaseView>.Ok(result.Value));

    private static async Task<IResult> SetImage(IMediator mediator, ClaimsPrincipal user, HttpContext http,
        ShowcaseImageSlot slot, ShowcaseImageRequest body, CancellationToken ct) =>
        Owner(http, await mediator.Send(new SetShowcaseImageCommand
        {
            Email = ShowcaseHttp.ProviderEmail(user),
            Slot = slot,
            Hash = body.Hash
        }, ct));

    private static async Task<IResult> SetHidden(IMediator mediator, ClaimsPrincipal user, HttpContext http,
        string providerRef, bool hidden, CancellationToken ct)
    {
        OwnershipGuard.AssertRole(user, "Customer");
        var result = await mediator.Send(new SetShowcaseHiddenCommand
        {
            CustomerEmail = ShowcaseHttp.CallerEmail(user),
            ProviderRef = providerRef,
            Hidden = hidden
        }, ct);
        return result.IsFailed ? ShowcaseHttp.Failure(http, result.Errors) : TypedResults.NoContent();
    }
}
