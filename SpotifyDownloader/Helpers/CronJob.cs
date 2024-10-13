using EasyCronJob.Abstractions;
using Microsoft.Extensions.Logging;
using SpotifyDownloader.Services;

namespace SpotifyDownloader.Helpers;

public class CronJob(ICronConfiguration<CronJob> cronConfiguration, ILogger<CronJob> logger,
    IFileManagmentService fileManagmentService, ITrackingService trackingService, IDownloadingService downloadingService)
    : CronJobService(cronConfiguration.CronExpression, cronConfiguration.TimeZoneInfo, cronConfiguration.CronFormat)
{
    public override async Task DoWork(CancellationToken cancellationToken)
    {
        logger.LogInformation("Job started");

        var trackingInformation = trackingService.ReadTrackingInformation();
        fileManagmentService.MigrateFromOlderVersion(trackingInformation);
        var result = await downloadingService.Download(trackingInformation);
        logger.LogInformation("Downloaded {albums} new albums and {playlists} new playlists.", result.AlbumsDownloaded, result.PlaylistsDownloaded);

        logger.LogInformation("Job finished");
    }
}