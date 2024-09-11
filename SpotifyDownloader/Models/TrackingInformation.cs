namespace SpotifyDownloader.Models;

public class TrackingInformation
{
    public List<Item> Artists { get; set; } = [];
    public List<Item> Playlists { get; set; } = [];

    public class Item
    {
        public required string Name { get; set; }
        public required string Url { get; set; }
        public bool Refresh { get; set; } = true;
    }
}

