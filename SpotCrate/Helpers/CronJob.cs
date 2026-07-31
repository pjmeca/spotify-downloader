using EasyCronJob.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SpotCrate.Services;

namespace SpotCrate.Helpers;

public class CronJob(ICronConfiguration<CronJob> cronConfiguration, ILogger<CronJob> logger, IServiceScopeFactory scopeFactory,
    ITrackingService trackingService, IYtDlpService ytDlpService,
    IFileOperationCoordinator fileOperationCoordinator)
    : CronJobService(cronConfiguration.CronExpression, cronConfiguration.TimeZoneInfo, cronConfiguration.CronFormat)
{
    public override async Task DoWork(CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Job started");
            await ytDlpService.EnsureReadyForRun(cancellationToken);

            using var scope = scopeFactory.CreateScope();
            var downloadingService = scope.ServiceProvider.GetRequiredService<IDownloadingService>();
            var artistsService = scope.ServiceProvider.GetRequiredService<IArtistsService>();

            var result = await fileOperationCoordinator.RunWithExclusiveMusicAccess(async () =>
            {
                var trackingInformation = await trackingService.ReadTrackingInformation(cancellationToken: cancellationToken);

                var result = await downloadingService.DownloadWithoutFileOperationLock(trackingInformation);

                logger.LogInformation("Updating the cache...");
                await artistsService.UpdateLocalArtistsInfo();

                return result;
            }, cancellationToken);
            logger.LogInformation("Downloaded {albums} new albums and {playlists} playlists.", result.AlbumsDownloaded, result.PlaylistsDownloaded);

            logger.LogInformation("Job finished");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An unhandled exception occurred during the cron job.");
        }
    }
}
