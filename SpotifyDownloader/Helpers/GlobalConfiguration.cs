using Microsoft.Extensions.Configuration;
using SpotifyDownloader.Utils;

namespace SpotifyDownloader.Helpers;

/// <summary>
/// Provides an easy way to access environment variables and appsettings.
/// </summary>
public class GlobalConfiguration
{
    public string CRON_SCHEDULE { get; }
    public string SPOTIFY_CLIENT_ID { get; }
    public string SPOTIFY_CLIENT_SECRET { get; }
    public string FORMAT { get; }
    public string? OPTIONS { get; }

    public const string MUSIC_DIRECTORY = "/music";
    public const string ARTISTS_DIRECTORY = $"{MUSIC_DIRECTORY}/Artists";
    public const string PLAYLISTS_DIRECTORY = $"{MUSIC_DIRECTORY}/Playlists";

    public const string DB_PATH = "/app/cache/application.db";

    public GlobalConfiguration(IConfiguration configuration)
    {
        CRON_SCHEDULE = configuration.GetValue<string>("CRON_SCHEDULE").ThrowExceptionIfNullOrWhiteSpace("CRON_SCHEDULE", false);
        SPOTIFY_CLIENT_ID = configuration.GetSection("CLIENT").GetValue<string>("ID").ThrowExceptionIfNullOrWhiteSpace("CLIENT__ID", false);
        SPOTIFY_CLIENT_SECRET = configuration.GetSection("CLIENT").GetValue<string>("SECRET").ThrowExceptionIfNullOrWhiteSpace("CLIENT__SECRET", false);
        FORMAT = configuration.GetValue<string>("FORMAT").ThrowExceptionIfNullOrWhiteSpace("FORMAT", false);
        OPTIONS = configuration.GetValue<string?>("OPTIONS").ValueOrNull()?.Trim();
    }
}
