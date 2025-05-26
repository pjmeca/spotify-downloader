namespace SpotifyDownloader.Models;

public enum PlaylistDownloadMode
{
     /// <summary>
     /// Only downloads new songs.
     /// </summary>
     Add = 0,
     /// <summary>
     /// Syncs the playlist entirely, removing songs that no longer exist in the source playlist.
     /// </summary>
     Full = 1
}