using Microsoft.Extensions.Configuration;

namespace AgendaBuddy.Provider.Core.Showcase;

/// <summary>Get-or-create: every call returns the same code, so a printed QR keeps working.</summary>
public class CreatePublicCodeCommandHandler(
    IShowcaseService showcaseService,
    IEventStore eventStore,
    IConfiguration configuration)
    : IRequestHandler<CreatePublicCodeCommand, Result<PublicCodeView>>
{
    public const string GoBaseUrlKey = "Showcase:Go:BaseUrl";

    public async Task<Result<PublicCodeView>> Handle(CreatePublicCodeCommand request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        var provider = await showcaseService.FindProviderByEmailAsync(request.Email);
        if (provider is null)
        {
            await eventStore.SaveAsync(ShowcaseProjection.Audit(nameof(CreatePublicCodeCommand), "Failed", request));
            return Result.Fail(ShowcaseError.NotFound());
        }

        var code = await showcaseService.GetOrCreatePublicCodeAsync(provider);
        var baseUrl = (configuration[GoBaseUrlKey] ?? string.Empty).TrimEnd('/');
        await eventStore.SaveAsync(ShowcaseProjection.Audit(nameof(CreatePublicCodeCommand), "Success",
            new { request.Email, code }));
        return Result.Ok(new PublicCodeView(code, $"{baseUrl}/api/v1/go/{code}"));
    }
}
