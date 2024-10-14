namespace SpotifyDownloader.Data;

public class Album
{
    public ulong Id { get; set; }
    public required string Name { get; set; }

    public virtual Artist Artist { get; set; } = null!;
}
