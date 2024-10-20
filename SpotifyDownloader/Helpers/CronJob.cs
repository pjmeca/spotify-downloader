﻿using EasyCronJob.Abstractions;
using Microsoft.Extensions.Logging;
using SpotifyDownloader.Services;

namespace SpotifyDownloader.Helpers;

public class CronJob(ICronConfiguration<CronJob> cronConfiguration, ILogger<CronJob> logger,
    IFileManagementService fileManagmentService, ITrackingService trackingService, IDownloadingService downloadingService,
    IArtistsService artistsService)
    : CronJobService(cronConfiguration.CronExpression, cronConfiguration.TimeZoneInfo, cronConfiguration.CronFormat)
{
    public override async Task DoWork(CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Job started");

            var trackingInformation = trackingService.ReadTrackingInformation();
            fileManagmentService.MigrateFromOlderVersion(trackingInformation);
            var result = await downloadingService.Download(trackingInformation);
            logger.LogInformation("Downloaded {albums} new albums and {playlists} playlists.", result.AlbumsDownloaded, result.PlaylistsDownloaded);

            logger.LogInformation("Updating the cache...");
            await artistsService.UpdateLocalArtistsInfo();

            logger.LogInformation("Job finished");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An unhandled exception occurred during the cron job.");
        }
    }
}