using SpotifyDownloader.Utils;

namespace SpotifyDownloader.Data;

public class Album
{
    public ulong Id { get; set; }

    public required string Name { get; set; }
    public string NormalizedName => Name.ToValidPathString();

    public virtual Artist Artist { get; set; } = null!;
}
