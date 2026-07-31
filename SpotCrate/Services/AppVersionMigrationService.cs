using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SpotCrate.Data;
using SpotCrate.Helpers;

namespace SpotCrate.Services;

public class AppVersionMigrationService(ApplicationDbContext dbContext, FileManagementService fileManagementService,
    TrackingService trackingService, FileOperationCoordinator fileOperationCoordinator,
    ILogger<AppVersionMigrationService> logger)
{
    public async Task MigrateToCurrentVersion(CancellationToken cancellationToken = default)
    {
        await fileOperationCoordinator.RunWithExclusiveMusicAccess(async () =>
        {
            var currentVersion = GlobalConfiguration.CurrentVersion;
            var latestVersion = await dbContext.AppVersions.FirstAsync(cancellationToken);
            if (currentVersion <= latestVersion)
            {
                return true;
            }

            logger.LogInformation("Migrating from latest version: {latestVersion}", latestVersion);
            var trackingInformation = await trackingService.ReadTrackingInformation(cancellationToken: cancellationToken);
            fileManagementService.MigrateFromOlderVersion(trackingInformation, latestVersion);
            await dbContext.AppVersions.ExecuteUpdateAsync(x =>
                x.SetProperty(x => x.Major, currentVersion.Major)
                    .SetProperty(x => x.Minor, currentVersion.Minor)
                    .SetProperty(x => x.Bugfix, currentVersion.Bugfix),
                cancellationToken);

            return true;
        }, cancellationToken);
    }
}
