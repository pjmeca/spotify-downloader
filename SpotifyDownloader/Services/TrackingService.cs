using Microsoft.Extensions.Logging;
using SpotifyDownloader.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SpotifyDownloader.Services;

interface ITrackingService
{
    TrackingInformation ReadTrackingInformation(string? trackingFile = null);
}

public class TrackingService(ILogger<TrackingService> logger) : ITrackingService
{
    public const string TrackingFile = "/app/tracking.yaml";

    public TrackingInformation ReadTrackingInformation(string? trackingFile = null)
    {
        trackingFile ??= TrackingFile;

        logger.LogInformation("Reading tracking information...");

        using StreamReader reader = new(trackingFile);
        string text = reader.ReadToEnd();

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();

        var trackingInformation = deserializer.Deserialize<TrackingInformation>(text);

        logger.LogInformation("Found {numArtists} artists and {numPlaylists} playlists.", trackingInformation.Artists.Count, trackingInformation.Playlists.Count);

        return trackingInformation;
    }
}