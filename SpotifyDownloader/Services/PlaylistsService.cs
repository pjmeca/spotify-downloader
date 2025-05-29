using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SpotifyAPI.Web;
using SpotifyDownloader.Data;
using SpotifyDownloader.Helpers;
using SpotifyDownloader.Models;
using SpotifyDownloader.Utils;
using static Fluents.Fluent;
using File = TagLib.File;

namespace SpotifyDownloader.Services;

public class PlaylistsService(ILogger<ArtistsService> logger, ISpotifyClientWrapper spotifyClient, ApplicationDbContext _applicationDbContext)
{
    public File[] GetLocalPlaylistInfo(string playlistName)
    {
        var itemDirectory = $"{GlobalConfiguration.PLAYLISTS_DIRECTORY}/{playlistName.ToValidPathString()}";

        // Get all the tracks in the directory
        var files= Directory.Exists(itemDirectory)
            ? Directory.GetFiles(itemDirectory, "*", SearchOption.AllDirectories)
            : [];
        
        return files
            .Select(x => Try(() => File.Create(x)).Ignore().Execute<File>())
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            .Where(x => x is not null)
            .Distinct()
            .ToArray();
    }

    public async Task<PlaylistTrack<FullTrack>[]> GetRemotePlaylistInfo(string url)
    {
        var playlistIdRegex = new Regex(@"/.*\.spotify.com\/.*playlist\/([^\?]+)(\?.+)?", RegexOptions.Compiled);
        var match = playlistIdRegex.Match(url);
        var playlistId = match.Success
            ? match.Groups[1].Value
            : null;

        if (playlistId is null)
        {
            logger.LogError("Playlist Id not found in URL: {url}", url);
            return [];
        }

        var tracks = await spotifyClient.GetAllPlaylistTracks(playlistId);
        
        // We need to convert the IPlayableItem to a FullTrack
        return tracks
            .Where(x => x.Track is FullTrack)
            .Select(x => new PlaylistTrack<FullTrack>()
            {
                AddedAt = x.AddedAt,
                AddedBy = x.AddedBy,
                IsLocal = x.IsLocal,
                Track = (FullTrack)x.Track,
            }).ToArray();
    }

    /// <summary>
    /// Updates a local playlist, removing those tracks that are no longer present in the remote playlist.
    /// </summary>
    public async Task SyncLocalPlaylist(TrackingInformation.PlaylistItem playlist)
    {
        if (playlist.Mode != PlaylistDownloadMode.Full)
        {
            return;
        }
        
        logger.LogInformation("Syncing the local playlist \"{name}\" with Spotify...", playlist.Name);
        
        // 1. Retrieve the local playlist
        var local = GetLocalPlaylistInfo(playlist.Name);
        
        // 2. Retrieve the remote playlist
        var remote = await GetRemotePlaylistInfo(playlist.Url);
        
        logger.LogInformation("The playlist \"{name}\" contains {numLocal} local tracks and {numRemote} remote tracks.",
            playlist.Name, local.Length, remote.Length);
        
        // 3. Filter the local tracks that do not exist in the remote playlist
        List<string> missingTracks = [];
        foreach (var file in local)
        {
            var existsRemoteTrack = remote
                .Any(x =>
                    x.Track.Artists.Select(x => x.Name).ToHashSet().SetEquals(file.Tag.Performers)
                    && x.Track.Name == file.Tag.Title);
            
            if (!existsRemoteTrack)
            {
                var fullPath = file.Name;
                var fileName = Path.GetFileNameWithoutExtension(fullPath);
                logger.LogInformation("The track \"{track}\" has been marked for deletion as it is no longer present in the remote playlist.", fileName);
                missingTracks.Add(fullPath);
            }
        }
        
        logger.LogInformation("Deleting {num} selected tracks...", missingTracks.Count);
        missingTracks.ForEach(x => Try(() => System.IO.File.Delete(x)).Ignore().Execute());
        logger.LogInformation("The playlist \"{name}\" has been synced with Spotify.", playlist.Name);
    }
}
