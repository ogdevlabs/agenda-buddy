namespace AgendaBuddy.Booking.Extensions;

/// <summary>
/// Records a session as completed once its time has passed.
/// </summary>
/// <remarks>
/// <para>
/// <b>Completion is a fact about the clock, not a claim somebody makes.</b> It used to be a button on the
/// appointment page: the provider had to remember to press it, and a session nobody pressed it for stayed
/// <c>Booked</c> for ever — so <c>ReportingService</c> counted long-finished work as outstanding, and a customer's
/// history never filled up. Removing the button without this would mean nothing ever completes at all.
/// </para>
/// <para>
/// A hosted service rather than a lazy check on read, because the STORED status is what matters:
/// <c>ReportingService</c> counts it from the provider's embedded list, and a status computed at render time would
/// be right on one screen and wrong in every report.
/// </para>
/// <para>
/// Both stores are updated per appointment — the <c>appointments</c> collection and the provider's embedded copy —
/// through the same two primitives cancellation uses. A single <c>UpdateMany</c> on the collection would be one
/// query instead of many, and would leave every embedded copy saying <c>Booked</c> indefinitely.
/// </para>
/// <para>
/// Failures are logged and swallowed. This runs forever in the background; an exception escaping the loop would
/// stop it silently for the rest of the process's life, and a database blip is not a reason to stop completing
/// sessions for ever.
/// </para>
/// </remarks>
public class AppointmentAutoCompletionService(
    IServiceScopeFactory scopeFactory,
    ILogger<AppointmentAutoCompletionService> logger) : BackgroundService
{
    /// <summary>
    /// How often to look. Fifteen minutes is well inside the granularity anyone perceives for "the session is
    /// over", and cheap: the query is indexed on status and matches nothing on most passes.
    /// </summary>
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Ceiling on one pass, so a long-neglected backlog — every session ever booked, on first deployment —
    /// cannot make a single tick unbounded. The remainder is picked up on the next pass.
    /// </summary>
    public const int BatchSize = 200;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // A short delay first: startup is busy, and nothing here is urgent to the second.
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var completed = await CompleteDueAppointmentsAsync(stoppingToken);

                // Only logged when something happened: a line every fifteen minutes saying "nothing to do" is
                // noise that buries the lines that matter.
                if (completed > 0)
                    logger.LogInformation("appointment.auto-completed: {Count} session(s)", completed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "appointment.auto-completion-failed: will retry next pass");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Completes every session whose time has passed, in both stores.
    /// </summary>
    /// <returns>How many were completed.</returns>
    /// <remarks>
    /// A scope per pass: <c>IBookingService</c> and <c>IProviderService</c> are scoped, and resolving them from
    /// the root provider would hold one Mongo-backed scope open for the process's lifetime.
    /// </remarks>
    private async Task<int> CompleteDueAppointmentsAsync(CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var bookings = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var providers = scope.ServiceProvider.GetRequiredService<IProviderService>();

        var nowUtc = DateTime.UtcNow;
        var due = await bookings.FindCompletableAppointmentsAsync(nowUtc, BatchSize);

        var completed = 0;

        foreach (var appointment in due)
        {
            if (stoppingToken.IsCancellationRequested) break;

            // Through the entity, so the transition rules are the same ones every other path obeys — Completed
            // is only reachable from Booked, and the description is refreshed with it.
            try
            {
                appointment.TransitionTo(AppointmentStatus.Completed);
            }
            catch (InvalidOperationException)
            {
                // Something changed it between the read and here. Not an error: the next pass will see whatever
                // it is now.
                continue;
            }

            var stored = await bookings.ChangeStatusAsync(
                appointment.Identifier,
                AppointmentStatus.Completed,
                appointment.AppointmentDescription);

            if (stored is null) continue;

            // The embedded copy is what ReportingService counts and what AvailabilityCalculator reads, so a
            // status written to only one of the two places leaves the dashboard reporting the old value.
            await providers.ChangeEmbeddedAppointmentStatusAsync(
                appointment.EmailProvider,
                appointment.Identifier,
                AppointmentStatus.Completed,
                appointment.AppointmentDescription);

            completed++;
        }

        return completed;
    }
}
