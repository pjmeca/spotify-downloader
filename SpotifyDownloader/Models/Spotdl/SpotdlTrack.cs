using SpotifyAPI.Web;

namespace SpotifyDownloader.Models.Spotdl;

public sealed class SpotdlTrack
{
    public string SpotifyId { get; init; } = string.Empty;
    public string SpotifyUrl { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<string> Artists { get; init; } = Array.Empty<string>();
    public string AlbumName { get; init; } = string.Empty;
    public string AlbumArtist { get; init; } = string.Empty;
    public int DiscNumber { get; init; }
    public int TrackNumber { get; init; }
    public int DurationSeconds { get; init; }
    public bool? Explicit { get; init; }
    public string? Isrc { get; init; }
    public uint? Year { get; init; }
    public string? CoverUrl { get; init; }

    public string Artist => Artists.Count > 0 ? Artists[0] : "Unknown";
    public string DisplayName => $"{Artist} - {Name}";

    /// <summary>
    /// Creates a normalized track model from a full Spotify track.
    /// </summary>
    public static SpotdlTrack FromFullTrack(FullTrack track)
    {
        var artists = track.Artists.Select(x => x.Name).ToList();
        var album = track.Album;
        var year = TryParseYear(album.ReleaseDate);
        var coverUrl = album.Images?.OrderByDescending(x => x.Width * x.Height).FirstOrDefault()?.Url;

        return new SpotdlTrack
        {
            SpotifyId = track.Id,
            SpotifyUrl = GetSpotifyUrl(track.ExternalUrls, track.Id),
            Name = track.Name,
            Artists = artists,
            AlbumName = album.Name,
            AlbumArtist = album.Artists.FirstOrDefault()?.Name ?? artists.FirstOrDefault() ?? "Unknown",
            DiscNumber = track.DiscNumber,
            TrackNumber = track.TrackNumber,
            DurationSeconds = track.DurationMs / 1000,
            Explicit = track.Explicit,
            Isrc = GetIsrc(track.ExternalIds),
            Year = year,
            CoverUrl = coverUrl
        };
    }

    /// <summary>
    /// Creates a normalized track model from a simple track and album.
    /// </summary>
    public static SpotdlTrack FromSimpleTrack(SimpleTrack track, SimpleAlbum album)
    {
        var artists = track.Artists.Select(x => x.Name).ToList();
        var year = TryParseYear(album.ReleaseDate);
        var coverUrl = album.Images?.OrderByDescending(x => x.Width * x.Height).FirstOrDefault()?.Url;

        return new SpotdlTrack
        {
            SpotifyId = track.Id,
            SpotifyUrl = GetSpotifyUrl(track.ExternalUrls, track.Id),
            Name = track.Name,
            Artists = artists,
            AlbumName = album.Name,
            AlbumArtist = album.Artists.FirstOrDefault()?.Name ?? artists.FirstOrDefault() ?? "Unknown",
            DiscNumber = track.DiscNumber,
            TrackNumber = track.TrackNumber,
            DurationSeconds = track.DurationMs / 1000,
            Explicit = track.Explicit,
            Isrc = null,
            Year = year,
            CoverUrl = coverUrl
        };
    }

    /// <summary>
    /// Extracts a release year from a Spotify release date.
    /// </summary>
    private static uint? TryParseYear(string? releaseDate)
    {
        if (string.IsNullOrWhiteSpace(releaseDate) || releaseDate.Length < 4)
        {
            return null;
        }

        return uint.TryParse(releaseDate[..4], out var year) ? year : null;
    }
    
    /// <summary>
    /// Safely resolves the Spotify track URL.
    /// </summary>
    private static string GetSpotifyUrl(IDictionary<string, string>? urls, string id)
    {
        if (urls != null && urls.TryGetValue("spotify", out var url) && !string.IsNullOrWhiteSpace(url))
        {
            return url;
        }

        return $"https://open.spotify.com/track/{id}";
    }

    /// <summary>
    /// Extracts the ISRC from Spotify external ids.
    /// </summary>
    private static string? GetIsrc(IDictionary<string, string>? externalIds)
    {
        if (externalIds != null && externalIds.TryGetValue("isrc", out var isrc) && !string.IsNullOrWhiteSpace(isrc))
        {
            return isrc;
        }

        return null;
    }
}