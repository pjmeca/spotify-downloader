using SpotifyDownloader.Utils;

namespace SpotifyDownloader.Models;

public class TrackingInformation
{
    public List<ArtistItem> Artists { get; set; } = [];
    public List<PlaylistItem> Playlists { get; set; } = [];

    public abstract class BaseItem
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

    public class ArtistItem : BaseItem;
    public class PlaylistItem : BaseItem
    {
        public PlaylistDownloadMode Mode { get; set; }
    }
}

