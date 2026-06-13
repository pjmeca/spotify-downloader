using Microsoft.Extensions.Logging;
using SpotifyDownloader.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SpotifyDownloader.Services;

public interface ITrackingService
{
    Task<TrackingInformation> ReadTrackingInformation(string? trackingFile = null, CancellationToken cancellationToken = default);
    Task WriteTrackingInformation(TrackingInformation trackingInformation, string? trackingFile = null, CancellationToken cancellationToken = default);
    Task<bool> IsTrackingFileWritable(string? trackingFile = null, CancellationToken cancellationToken = default);
}

public class TrackingService(ILogger<TrackingService> logger) : ITrackingService
{
    public const string DEFAULT_TRACKING_FILE = "/app/tracking.yaml";
    private readonly SemaphoreSlim trackingFileSemaphore = new(1, 1);

    public async Task<TrackingInformation> ReadTrackingInformation(string? trackingFile = null, CancellationToken cancellationToken = default)
    {
        trackingFile ??= DEFAULT_TRACKING_FILE;

        logger.LogInformation("Reading tracking information...");

        await trackingFileSemaphore.WaitAsync(cancellationToken);
        try
        {
            string text = await System.IO.File.ReadAllTextAsync(trackingFile, cancellationToken);

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .WithTypeConverter(new PlaylistDownloadModeYamlConverter())
                .Build();

            var trackingInformation = deserializer.Deserialize<TrackingInformation>(text) ?? new TrackingInformation();
            trackingInformation.Artists ??= [];
            trackingInformation.Playlists ??= [];

            logger.LogInformation("Found {numArtists} artists and {numPlaylists} playlists.", trackingInformation.Artists.Count, trackingInformation.Playlists.Count);

            return trackingInformation;
        }
        finally
        {
            trackingFileSemaphore.Release();
        }
    }

    public async Task WriteTrackingInformation(TrackingInformation trackingInformation, string? trackingFile = null, CancellationToken cancellationToken = default)
    {
        trackingFile ??= DEFAULT_TRACKING_FILE;

        logger.LogInformation("Writing tracking information...");

        var serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .WithTypeConverter(new PlaylistDownloadModeYamlConverter())
            .WithIndentedSequences()
            .DisableAliases()
            .Build();

        var yaml = serializer.Serialize(trackingInformation);
        var directory = Path.GetDirectoryName(trackingFile) ?? Directory.GetCurrentDirectory();
        Directory.CreateDirectory(directory);

        await trackingFileSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (System.IO.File.Exists(trackingFile))
            {
                await System.IO.File.WriteAllTextAsync(trackingFile, yaml);
            }
            else
            {
                var tempFile = Path.Combine(directory, $".{Path.GetFileName(trackingFile)}.{Guid.NewGuid():N}.tmp");
                await System.IO.File.WriteAllTextAsync(tempFile, yaml);
                System.IO.File.Move(tempFile, trackingFile, true);
            }
        }
        finally
        {
            trackingFileSemaphore.Release();
        }

        logger.LogInformation("Tracking information written to {trackingFile}.", trackingFile);
    }

    public async Task<bool> IsTrackingFileWritable(string? trackingFile = null, CancellationToken cancellationToken = default)
    {
        trackingFile ??= DEFAULT_TRACKING_FILE;

        await trackingFileSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (System.IO.File.Exists(trackingFile))
            {
                using var stream = System.IO.File.Open(trackingFile, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
                return stream.CanWrite;
            }

            var directory = Path.GetDirectoryName(trackingFile) ?? Directory.GetCurrentDirectory();
            Directory.CreateDirectory(directory);
            var tempFile = Path.Combine(directory, $".{Path.GetFileName(trackingFile)}.{Guid.NewGuid():N}.tmp");
            System.IO.File.WriteAllText(tempFile, string.Empty);
            System.IO.File.Delete(tempFile);
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or DirectoryNotFoundException)
        {
            logger.LogWarning(ex, "Tracking file {trackingFile} does not appear to be writable.", trackingFile);
            return false;
        }
        finally
        {
            trackingFileSemaphore.Release();
        }
    }
}
