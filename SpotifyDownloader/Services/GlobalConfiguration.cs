using Microsoft.Extensions.Configuration;
using SpotifyDownloader.Utils;

namespace SpotifyDownloader.Services;

/// <summary>
/// Provides an easy way to access environment variables and appsettings.
/// </summary>
public class GlobalConfiguration
{
    public string CRON_SCHEDULE { get; }
    public string SPOTIFY_CLIENT_ID { get; }
    public string SPOTIFY_CLIENT_SECRET { get; }

    public GlobalConfiguration(IConfiguration configuration)
    {
        CRON_SCHEDULE = configuration.GetValue<string>("CRON_SCHEDULE").ThrowExceptionIfNullOrWhiteSpace(nameof(CRON_SCHEDULE));
        SPOTIFY_CLIENT_ID = configuration.GetSection("CLIENT").GetValue<string>("ID").ThrowExceptionIfNullOrWhiteSpace(nameof(SPOTIFY_CLIENT_ID));
        SPOTIFY_CLIENT_SECRET = configuration.GetSection("CLIENT").GetValue<string>("SECRET").ThrowExceptionIfNullOrWhiteSpace(nameof(SPOTIFY_CLIENT_SECRET));
    }
}
