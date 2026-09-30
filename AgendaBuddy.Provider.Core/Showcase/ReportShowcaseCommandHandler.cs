using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AgendaBuddy.Provider.Core.Showcase;

/// <summary>
/// Files a report and emails the operator, best-effort: the stored row is the record, and a dead mail provider
/// must not turn a report into an error the reporter retries. A second report of the same provider by the same
/// person within 24 hours is accepted without writing.
/// </summary>
public class ReportShowcaseCommandHandler(
    IShowcaseService showcaseService,
    IEmailSender emailSender,
    IEventStore eventStore,
    IConfiguration configuration,
    ILogger<ReportShowcaseCommandHandler> logger)
    : IRequestHandler<ReportShowcaseCommand, Result>
{
    public const string ReportEmailKey = "Showcase:ReportEmail";

    public async Task<Result> Handle(ReportShowcaseCommand request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        var reason = request.Reason.Trim().ToLowerInvariant();
        var detail = ShowcaseRules.Clean(request.Detail);
        if (!ShowcaseReportReasons.All.Contains(reason))
            return await FailAsync(request, ShowcaseError.Invalid("reason", "invalid-reason", "Choose a reason."));
        if (!ShowcaseRules.FitsWithin(detail, ShowcaseRules.MaxReportDetail))
            return await FailAsync(request, ShowcaseError.Invalid("detail", "too-long",
                $"Keep the detail to {ShowcaseRules.MaxReportDetail} characters."));
        if (reason == "other" && detail is null)
            return await FailAsync(request, ShowcaseError.Invalid("detail", "required", "Tell us what's wrong."));

        var provider = await showcaseService.FindProviderByRefAsync(request.ProviderRef);
        var showcase = provider is null ? null : await showcaseService.FindShowcaseAsync(provider.Id);
        if (provider is null
            || ShowcaseService.IsOwner(provider, request.ReporterEmail)
            || !await showcaseService.IsVisibleToAsync(provider, showcase, request.ReporterEmail))
            return await FailAsync(request, ShowcaseError.NotFound());

        var portfolioHash = ShowcaseRules.IsHash(request.PortfolioHash) ? request.PortfolioHash : null;
        var stored = await showcaseService.ReportAsync(provider.Id, request.ReporterEmail, reason, detail, portfolioHash);
        if (stored)
            await NotifyOperatorAsync(provider, reason, detail, portfolioHash, cancellationToken);

        await eventStore.SaveAsync(ShowcaseProjection.Audit(nameof(ReportShowcaseCommand), "Success",
            new { request.ReporterEmail, request.ProviderRef, reason, stored }));
        return Result.Ok();
    }

    private async Task NotifyOperatorAsync(
        ProviderEntity provider, string reason, string? detail, string? portfolioHash, CancellationToken cancellationToken)
    {
        var to = configuration[ReportEmailKey];
        if (string.IsNullOrWhiteSpace(to))
        {
            logger.LogWarning("A showcase report was stored but {Key} is not configured, so no operator was emailed", ReportEmailKey);
            return;
        }

        var body =
            $"A showcase was reported.\n\nProvider ref: {provider.Id}\nReason: {reason}\n" +
            $"Image: {portfolioHash ?? "(whole showcase)"}\nDetail: {detail ?? "(none)"}\n\n" +
            "To take it down, set hidden_by_operator: true on its provider_showcase document.";
        try
        {
            await emailSender.SendAsync(to, "AgendaMe showcase report", body, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not email the showcase report to the operator");
        }
    }

    private async Task<Result> FailAsync(ReportShowcaseCommand request, ShowcaseError error)
    {
        await eventStore.SaveAsync(ShowcaseProjection.Audit(nameof(ReportShowcaseCommand), "Failed",
            new { request.ReporterEmail, request.ProviderRef, request.Reason }));
        return Result.Fail(error);
    }
}
