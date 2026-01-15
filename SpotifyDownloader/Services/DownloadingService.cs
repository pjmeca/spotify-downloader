using Microsoft.Extensions.Logging;
using SpotifyAPI.Web;
using SpotifyDownloader.Helpers;
using SpotifyDownloader.Models;
using SpotifyDownloader.Utils;
using static Fluents.Fluent;

namespace SpotifyDownloader.Services;

public interface IDownloadingService
{
    Task<DownloadResult> Download(TrackingInformation trackingInformation);
}

public class DownloadingService(ILogger<DownloadingService> logger,
    ISpotifyClientWrapper spotifyClient, IArtistsService artistsService, PlaylistsService playlistsService,
    ISpotdlService spotdlService) : IDownloadingService
{
    public async Task<DownloadResult> Download(TrackingInformation trackingInformation)
    {
        DownloadResult result = new();

        try
        {
            // Let's see what's currently in the music directory
            IEnumerable<string> folders = Directory.GetDirectories(GlobalConfiguration.MUSIC_DIRECTORY);
            folders = folders.Select(x => x.Split("/")[^1]);

            // Remove those items that already exist and have refresh set to false
            trackingInformation.Artists.RemoveAll(x => !x.Refresh && folders.Contains(x.Name));
            trackingInformation.Playlists.RemoveAll(x => !x.Refresh && folders.Contains(x.Name));

            foreach (var artist in trackingInformation.Artists)
            {
                try
                {
                    logger.LogInformation("Processing the artist \"{name}\"", artist.Name);
                    result.AlbumsDownloaded += await ProcessArtist(artist);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An exception occurred while processing the artist \"{item}\".", artist.Name);
                }
            }

            foreach (var playlist in trackingInformation.Playlists)
            {
                try
                {
                    logger.LogInformation("Processing the playlist \"{name}\"", playlist.Name);
                    await ProcessPlaylist(playlist);
                    result.PlaylistsDownloaded++;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An exception occurred while processing the playlist \"{item}\".", playlist.Name);
                }
            }
        }
        finally
        {
            CleanupTemporaryDownloads();
        }

        return result;
    }
    
    private async Task<int> ProcessArtist(TrackingInformation.ArtistItem artist)
    {
        string itemDirectory = $"{GlobalConfiguration.ARTISTS_DIRECTORY}/{artist.Name.ToValidPathString()}";

        (var localTracks, var localAlbums) = await artistsService.GetLocalArtistInfo(artist.Name);
        logger.LogInformation("Currently there are {numAlbums} albums with a total number of {numTracks} tracks.", localAlbums.Length, localTracks.Length);
        
        var remoteAlbums = await artistsService.GetRemoteArtistInfo(artist.Url);
        var albumsToDownload = remoteAlbums
            .Where(x => x.AlbumType != "compilation") // AlbumType allowed values: "album", "single", "compilation"
            .Where(x => !Array.Exists(localAlbums, y => y.Equals(x.Name.ToValidPathString(), StringComparison.InvariantCultureIgnoreCase)))
            .OrderBy(x => x.ReleaseDate)
            .ToList();

        albumsToDownload = ClearRepeatedSingles(albumsToDownload, localTracks);

        logger.LogInformation("{num} albums will be downloaded.", albumsToDownload.Count);
        int albumsDownloaded = albumsToDownload.Count;

        // Process "appears_on" independently
        var albumsAppearsOnToDownload = albumsToDownload
            .Where(x => x.AlbumGroup == "appears_on")
            .ToList();
        albumsToDownload.RemoveAll(x => albumsAppearsOnToDownload.Contains(x));

        foreach(var album in albumsToDownload)
        {
            var result = await DownloadAlbum(itemDirectory, album);
            if (!result)
            {
                albumsDownloaded--;
            }
        }

        foreach (var album in albumsAppearsOnToDownload)
        {
            var result = await DownloadTracksFromAlbum(itemDirectory, album,
                x => x.Artists.Select(x => x.Name).Any(x => x.Contains(artist.Name)));
            if (!result)
            {
                albumsDownloaded--;
            }
        }

        return albumsDownloaded;

        static List<SimpleAlbum> ClearRepeatedSingles(List<SimpleAlbum> albums, string[] localTracks)
        {
            // When a single is released in multiple albums, the track gets stuck
            // because, even though the album is different, the track name is the same,
            // so the downloader skips it and never downloads it. As a result, the album is retrieved
            // for download again in subsequent executions.
            // This method tries to prevent this cases as much as possible without calling the Spotify API.
            return albums
                .Where(album => album.TotalTracks > 1 || !Array.Exists(localTracks, localTrack => localTrack.Contains(album.Name)))
                .ToList();
        }
    }

    /// <summary>
    /// Processes a Spotify playlist by downloading all its tracks and syncing it locally (if needed).
    /// </summary>
    private async Task ProcessPlaylist(TrackingInformation.PlaylistItem playlist)
    {
        string itemDirectory = $"{GlobalConfiguration.PLAYLISTS_DIRECTORY}/{playlist.Name}";
        Directory.CreateDirectory(itemDirectory);
        var existingTracks = Directory.GetFiles(itemDirectory).Select(x => Path.GetFileNameWithoutExtension(x.Split("/")[^1])).ToArray();
        
        // Get all the remote tracks
        var remoteTracks = await playlistsService.GetRemotePlaylistInfo(playlist.Url);

        foreach (var remoteTrack in remoteTracks)
        {
            try
            {
                // Read the artists (for logging purposes)
                var artists = remoteTrack.Track.Artists.Select(x => x.Name).ToArray();
                var spotDlFileName = $"{string.Join(", ", artists)} - {remoteTrack.Track.Name}".ToValidPathString();
            
                // Skip this track if already downloaded
                if (existingTracks.Contains(spotDlFileName))
                {
                    logger.LogInformation("The track \"{name}\" already exists. Skipping...", spotDlFileName);
                    continue;
                }
                
                // Download this track
                await spotdlService.DownloadTrack(itemDirectory, remoteTrack.Track);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An exception occurred while downloading the track \"{trackName}\" for the playlist \"{playlistName}\".",
                    remoteTrack.Track.Name, playlist.Name);
            }
        }
        
        await playlistsService.SyncLocalPlaylist(playlist, remoteTracks);
    }

    private async Task<bool> DownloadAlbum(string path, SimpleAlbum album)
    {
        string downloadPath = album.TotalTracks > 1 ?
            $"{path}/{album.Name.ToValidPathString()}"
            : path;
        try
        {
            await spotdlService.DownloadAlbum(downloadPath, album);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An exception occurred while downloading the album \"{album}\".", album.Name);

            // Remove all downloaded songs from this album so it will be downloaded next time
            Directory.GetFiles(downloadPath)
                .Where(x => Try(() => TagLib.File.Create(x).Tag.Album).Ignore().Execute<string>() == album.Name)
                .ToList()
                .ForEach(File.Delete);

            return false;
        }
    }

    private async Task<bool> DownloadTracksFromAlbum(string path, SimpleAlbum album, Func<SimpleTrack, bool>? filter = null)
    {
        if (filter is null)
        {
            return await DownloadAlbum(path, album);
        }

        string? downloadPath = null;

        try
        {
            var albumTracks = await spotifyClient.GetAllAlbumTracks(album.Id);

            var tracksToDownload = albumTracks.Where(x => filter(x)).ToList();
            downloadPath = tracksToDownload.Count > 1 ?
                $"{path}/{album.Name.ToValidPathString()}"
                : path;

            foreach (var track in tracksToDownload)
            {
                await spotdlService.DownloadTrack(downloadPath, track, album);
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An exception occurred while downloading tracks from the album \"{album}\".", album.Name);

            // Remove all downloaded songs from this album so it will be downloaded next time
            if (downloadPath != null)
            {
                Directory.GetFiles(downloadPath)
                    .Where(x => Try(() => TagLib.File.Create(x).Tag.Album).Ignore().Execute<string>() == album.Name)
                    .ToList()
                    .ForEach(File.Delete);
            }

            return false;
        }
    }

    private void CleanupTemporaryDownloads()
    {
        var tempDirectory = Path.Combine(GlobalConfiguration.MUSIC_DIRECTORY, ".tmp");
        if (!Directory.Exists(tempDirectory))
        {
            return;
        }

        try
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to clean temporary downloads directory: {path}", tempDirectory);
        }
    }

}
