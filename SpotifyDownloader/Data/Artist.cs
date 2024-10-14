namespace SpotifyDownloader.Data;

public class Artist
{
    public ulong Id { get; set; }
    public required string Name { get; set; }

    public virtual List<Album> Albums { get; set; } = [];
}