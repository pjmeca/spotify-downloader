using EasyCronJob.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SpotifyDownloader.Data;
using SpotifyDownloader.Services;

namespace SpotifyDownloader.Helpers;

public class CronJob(ICronConfiguration<CronJob> cronConfiguration, ILogger<CronJob> logger, IServiceScopeFactory scopeFactory,
    IFileManagementService fileManagmentService, ITrackingService trackingService, IYtDlpService ytDlpService,
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
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var downloadingService = scope.ServiceProvider.GetRequiredService<IDownloadingService>();
            var artistsService = scope.ServiceProvider.GetRequiredService<IArtistsService>();

            var result = await fileOperationCoordinator.RunWithExclusiveMusicAccess(async () =>
            {
                var trackingInformation = await trackingService.ReadTrackingInformation(cancellationToken: cancellationToken);
                
                var currentVersion = GlobalConfiguration.CurrentVersion;
                var latestVersion = await dbContext.AppVersions.FirstAsync(cancellationToken);
                if (currentVersion > latestVersion)
                {
                    logger.LogInformation("Migrating from latest version: {latestVersion}", latestVersion);
                    fileManagmentService.MigrateFromOlderVersion(trackingInformation, latestVersion);
                    await dbContext.AppVersions.ExecuteUpdateAsync(x =>
                        x.SetProperty(x => x.Major, currentVersion.Major).SetProperty(x => x.Minor, currentVersion.Minor).SetProperty(x => x.Bugfix, currentVersion.Bugfix),
                        cancellationToken);
                }

                var result = await downloadingService.DownloadWithoutFileOperationLock(trackingInformation);

                logger.LogInformation("Updating the cache...");
                await artistsService.UpdateLocalArtistsInfo();

                return result;
            });
            logger.LogInformation("Downloaded {albums} new albums and {playlists} playlists.", result.AlbumsDownloaded, result.PlaylistsDownloaded);

            logger.LogInformation("Job finished");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An unhandled exception occurred during the cron job.");
        }
    }
}
