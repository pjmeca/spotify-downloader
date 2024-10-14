using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using SpotifyAPI.Web;
using SpotifyDownloader.Helpers;
using SpotifyDownloader.Models;
using SpotifyDownloader.Utils;

namespace SpotifyDownloader.Services;

public interface IDownloadingService
{
    Task<DownloadResult> Download(TrackingInformation trackingInformation);
}

public class DownloadingService(ILogger<DownloadingService> logger, GlobalConfiguration configuration,
    SpotifyClient spotifyClient, IArtistsService _artistsService) : IDownloadingService
{
    public async Task<DownloadResult> Download(TrackingInformation trackingInformation)
    {
        DownloadResult result = new();

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

        return result;
    }
    
    private async Task<int> ProcessArtist(TrackingInformation.Item artist)
    {
        string itemDirectory = $"{GlobalConfiguration.ARTISTS_DIRECTORY}/{artist.Name.ToValidPathString()}";

        (var localTracks, var localAlbums) = await _artistsService.GetLocalArtistInfo(artist.Name);
        logger.LogInformation("Currently there are {numAlbums} albums with a total number of {numTracks} tracks.", localAlbums.Length, localTracks.Length);
        
        var remoteAlbums = await _artistsService.GetRemoteArtistInfo(artist.Url);
        var albumsToDownload = remoteAlbums
            .Where(x => x.AlbumType != "compilation") // AlbumType allowed values: "album", "single", "compilation"
            .Where(x => !Array.Exists(localAlbums, y => y.Equals(x.Name.ToValidPathString(), StringComparison.InvariantCultureIgnoreCase)))
            .OrderBy(x => x.ReleaseDate)
            .ToList();

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
    }

    private async Task ProcessPlaylist(TrackingInformation.Item playlist)
    {
        string itemDirectory = $"{GlobalConfiguration.PLAYLISTS_DIRECTORY}/{playlist.Name}";

        _ = await DownloadPlaylist(itemDirectory, playlist.Name, playlist.Url);
    }

    private async Task<bool> DownloadAlbum(string path, SimpleAlbum album)
    {
        string downloadPath = album.TotalTracks > 1 ?
            $"{path}/{album.Name.ToValidPathString()}"
            : path;
        try
        {
            await Download(downloadPath, "album", album.Name, album.ExternalUrls["spotify"]);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An exception occurred while downloading the album \"{album}\".", album.Name);

            // Remove all downloaded songs from this album so it will be downloaded next time
            Directory.GetFiles(downloadPath)
                .Where(x => TagLib.File.Create(x).Tag.Album == album.Name)
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
            var firstTrack = await spotifyClient.Albums.GetTracks(album.Id);
            var albumTracks = await spotifyClient.PaginateAll(firstTrack);

            var tracksToDownload = albumTracks.Where(x => filter(x)).ToList();
            downloadPath = tracksToDownload.Count > 1 ?
                $"{path}/{album.Name.ToValidPathString()}"
                : path;

            foreach (var track in tracksToDownload)
            {
                await Download(downloadPath, "track", track.Name, track.ExternalUrls["spotify"]);
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
                    .Where(x => TagLib.File.Create(x).Tag.Album == album.Name)
                    .ToList()
                    .ForEach(File.Delete);
            }

            return false;
        }
    }

    private async Task<bool> DownloadPlaylist(string path, string name, string url)
    {
        try
        {
            await Download($"{path}/{name.ToValidPathString()}", "playlist", name, url);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An exception occurred while downloading the playlist \"{playlist}\".", name);
            return false;
        }
    }

    private async Task Download(string path, string type, string name, string url)
    {
        logger.LogInformation("Downloading the {type} \"{name}\" with spotdl.", type, name);

        Directory.CreateDirectory(path);

        var arguments = new StringBuilder()
            .Append($"download {url}")
            .Append($" --format {configuration.FORMAT}")
            .Append($" --threads {Process.GetCurrentProcess().Threads.Count}")
            .Append($" --client-id {configuration.SPOTIFY_CLIENT_ID} --client-secret {configuration.SPOTIFY_CLIENT_SECRET}");
        if (configuration.OPTIONS is not null)
        {
            arguments.Append($" {configuration.OPTIONS}");
        }

        ProcessStartInfo startInfo = new()
        {
            WorkingDirectory = path,
            FileName = @"/env/bin/spotdl",
            Arguments = arguments.ToString(),
            UseShellExecute = false,
            RedirectStandardOutput = true
        };

        using Process? process = Process.Start(startInfo);
        if (process != null)
        {
            string output = await process.StandardOutput.ReadToEndAsync();
            foreach (var x in output.Split("\n").Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                logger.LogInformation("{output}", x);
            }

            await process.WaitForExitAsync();
        }

        logger.LogInformation("Downloaded \"{name}\".", name);
    }
}
