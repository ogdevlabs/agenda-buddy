using AgendaBuddy.Library.Media;

namespace AgendaBuddy.Provider.Api.Showcase;

/// <summary>Hourly: removes images nothing has referenced for 24 hours, and blobs whose provider no longer exists.</summary>
public sealed class MediaSweepService(IServiceScopeFactory scopeFactory, ILogger<MediaSweepService> logger)
    : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var summary = await scope.ServiceProvider.GetRequiredService<MediaSweeper>().SweepAsync(stoppingToken);
                if (summary.DetachedRemoved + summary.OrphanedPrefixesRemoved > 0)
                    logger.LogInformation("Media sweep removed {Detached} detached images and {Orphaned} orphaned prefixes",
                        summary.DetachedRemoved, summary.OrphanedPrefixesRemoved);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Media sweep failed; it will run again in {Interval}", Interval);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
