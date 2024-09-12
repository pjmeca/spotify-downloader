using EasyCronJob.Abstractions;
using Microsoft.Extensions.Logging;

namespace SpotifyDownloader.Services;

public class CronJob(ICronConfiguration<CronJob> cronConfiguration, ILogger<CronJob> logger,
    ITrackingService trackingService, IDownloadingService downloadingService)
    : CronJobService(cronConfiguration.CronExpression, cronConfiguration.TimeZoneInfo, cronConfiguration.CronFormat)
{
    public override async Task DoWork(CancellationToken cancellationToken)
    {
        var trackingInformation = trackingService.ReadTrackingInformation();
        var result = await downloadingService.Download(trackingInformation);
        logger.LogInformation("Downloaded {albums} new albums and {playlists} new playlists.", result.AlbumsDownloaded, result.PlaylistsDownloaded);
    }
}