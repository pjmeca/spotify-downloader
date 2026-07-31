using SpotCrate.Utils;
using YamlDotNet.Serialization;

namespace SpotCrate.Models;

public class TrackingInformation
{
    public List<ArtistItem> Artists { get; set; } = [];
    public List<PlaylistItem> Playlists { get; set; } = [];

    public abstract class BaseItem
    {
        private string _name = null!;
        [YamlMember(Order = 1)]
        public required string Name
        {
            get => _name;
            set
            {
                _name = value.ToValidPathString();
            }
        }
        [YamlMember(Order = 2)]
        public required string Url { get; set; }
        [YamlMember(Order = 3)]
        public bool Refresh { get; set; } = true;
    }

    public class ArtistItem : BaseItem;
    public class PlaylistItem : BaseItem
    {
        [YamlMember(Order = 4)]
        public PlaylistDownloadMode Mode { get; set; }
    }
}
