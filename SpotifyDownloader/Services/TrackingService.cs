using Microsoft.Extensions.Logging;
using SpotifyDownloader.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SpotifyDownloader.Services;

public interface ITrackingService
{
    TrackingInformation ReadTrackingInformation(string? trackingFile = null);
    void WriteTrackingInformation(TrackingInformation trackingInformation, string? trackingFile = null);
    bool IsTrackingFileWritable(string? trackingFile = null);
}

public class TrackingService(ILogger<TrackingService> logger) : ITrackingService
{
    public const string DEFAULT_TRACKING_FILE = "/app/tracking.yaml";

    public TrackingInformation ReadTrackingInformation(string? trackingFile = null)
    {
        trackingFile ??= DEFAULT_TRACKING_FILE;

        logger.LogInformation("Reading tracking information...");

        using StreamReader reader = new(trackingFile);
        string text = reader.ReadToEnd();

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

    public void WriteTrackingInformation(TrackingInformation trackingInformation, string? trackingFile = null)
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

        if (System.IO.File.Exists(trackingFile))
        {
            System.IO.File.WriteAllText(trackingFile, yaml);
        }
        else
        {
            var tempFile = Path.Combine(directory, $".{Path.GetFileName(trackingFile)}.{Guid.NewGuid():N}.tmp");
            System.IO.File.WriteAllText(tempFile, yaml);
            System.IO.File.Move(tempFile, trackingFile, true);
        }

        logger.LogInformation("Tracking information written to {trackingFile}.", trackingFile);
    }

    public bool IsTrackingFileWritable(string? trackingFile = null)
    {
        trackingFile ??= DEFAULT_TRACKING_FILE;

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
    }
}
