using SpotifyDownloader.Utils;

namespace SpotifyDownloader.Data;

public class Artist
{
    public ulong Id { get; set; }

    public required string Name { get; set; }
    public string NormalizedName => Name.ToValidPathString();

    public virtual List<Album> Albums { get; set; } = [];
}