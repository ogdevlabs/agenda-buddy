using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace AgendaBuddy.Library.Tools;

/// <summary>
/// Maps <see cref="ForbiddenException"/> to <b>403</b> in every environment, whether or not the endpoint
/// remembered a local <c>try/catch</c>.
/// </summary>
/// <remarks>
/// <para>
/// F-016 AC-13, AC-14 and AC-23 (`[security]`, threat <b>T-004</b>). Implements ADR-022 /
/// <c>ARCHITECTURE.md</c> AD-1.
/// </para>
/// <para>
/// <b>Why a new mechanism rather than editing the existing handler.</b> PRD requirement 14 asked for the
/// mapping to be added centrally, and it could not be done where the PRD assumed. In all seven services
/// <c>UseExceptionHandler</c> is registered <em>inside</em> <c>if (app.Environment.IsDevelopment())</c>,
/// next to Swagger (<c>10-error-handling.md:9-34</c>). A branch added to that lambda would give 403 in
/// Development and a bare, empty-bodied <b>500 in Production</b> — the exact silent-signalling failure
/// requirement 14 exists to remove, preserved in the only environment that matters.
/// </para>
/// <para>
/// This is the first <see cref="IExceptionHandler"/> in the codebase. It is registered with
/// <c>AddExceptionHandler&lt;AgendaBuddyExceptionHandler&gt;()</c> and <c>app.UseExceptionHandler()</c> in
/// the six domain services.
/// </para>
/// <para>
/// ⚠️ <b><c>app.UseExceptionHandler()</c> must be registered AFTER the <c>IsDevelopment()</c> block.</b>
/// Middleware registered earlier is outermost, and an exception propagates outward, so the
/// <em>innermost</em> handler sees it first. Registered after, this handler is inner to the Development
/// lambda: it takes <see cref="ForbiddenException"/>, and returns <c>false</c> for everything else so the
/// exception rethrows and propagates out to that lambda, leaving today's behaviour untouched. Register it
/// <em>before</em> the block and the Development lambda becomes innermost, swallows
/// <see cref="ForbiddenException"/>, and AC-13 fails <b>in Development only</b> — green in Production and
/// red on the developer's machine, the worst way round to discover it. Pinned by
/// <c>CentralForbiddenTest</c>, which asserts both environments.
/// </para>
/// <para>
/// <b>Deliberately not mapped:</b> the nine other exception types that still surface as 500
/// (<c>api-contracts.md</c> §3.3) — <c>FormatException</c> being the most likely live one. Each would
/// change an untouched endpoint's contract and no acceptance criterion covers them, so they are left
/// alone (ADR-022). Each is a one-line addition here when a criterion exists.
/// </para>
/// <para>
/// <b>Not registered in Identity</b>, which uses an incompatible ad-hoc <c>{ error, message }</c> envelope
/// and is the only service without <c>ProblemDetailsServiceEndpointFilter</c>
/// (<c>10-error-handling.md:146,208</c>). Two error schemes in one service would be worse than the
/// inconsistency. F-021 touches Identity next.
/// </para>
/// </remarks>
/// <param name="problemDetailsService">
/// Used rather than writing JSON directly, so each service's <c>CustomizeProblemDetails</c> extension
/// runs and supplies <c>requestId</c> from <c>Activity.Current?.Id</c>.
/// </param>
public sealed class AgendaBuddyExceptionHandler(IProblemDetailsService problemDetailsService)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not ForbiddenException)
        {
            // Declined, not swallowed. The middleware rethrows and the Development-only lambda handles
            // it exactly as it does today.
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;

        // T-004: status, title and requestId only. Nothing derived from the exception is passed in —
        // no Detail, no Exception, no metadata. The omission IS the control; there is nothing to
        // sanitise later.
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails =
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Forbidden",
            },
        });
    }
}
