using SpotifyDownloader.Utils;

namespace SpotifyDownloader.Models;

public class TrackingInformation
{
    public List<Item> Artists { get; set; } = [];
    public List<Item> Playlists { get; set; } = [];

    public class Item
    {
        private string _name = null!;
        public required string Name
        {
            get => _name;
            set
            {
                _name = value.ToValidPathString();
            }
        }
        public required string Url { get; set; }
        public bool Refresh { get; set; } = true;
    }
}

