using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SpotifyAPI.Web;
using SpotifyDownloader.Helpers;
using static SpotifyAPI.Web.ArtistsAlbumsRequest;

namespace SpotifyDownloader.Services;

public interface IArtistsService
{
    (string[] localTracks, string[] localAlbums) GetLocalArtistInfo(string artistName);

    Task<SimpleAlbum[]> GetRemoteArtistInfo(string url);
}

public class ArtistsService(ILogger<ArtistsService> logger, SpotifyClient spotifyClient) : IArtistsService
{
    public (string[] localTracks, string[] localAlbums) GetLocalArtistInfo(string artistName)
    {
        var itemDirectory = $"{GlobalConfiguration.ARTISTS_DIRECTORY}/{artistName}";

        string[] localTracks = [];
        string[] localAlbums = [];

        // Get all the tracks in the directory
        if (Directory.Exists(itemDirectory))
        {
            localTracks = Directory.GetFiles(itemDirectory, "*", SearchOption.AllDirectories);
            localAlbums = localTracks
                .Select(x =>
                {
                    var file = TagLib.File.Create(x);
                    return file.Tag.Album;
                })
                .Distinct()
                .ToArray();
        }

        return (localTracks, localAlbums);
    }

    public async Task<SimpleAlbum[]> GetRemoteArtistInfo(string url)
    {
        var artistIdRegex = new Regex(@"/.*\.spotify.com\/.*artist\/([^\?]+)(\?.+)?", RegexOptions.Compiled);
        var artistId = artistIdRegex.Match(url).Groups[1].Value;

        if (artistId == null)
        {
            logger.LogError("Artist not found in URL: {url}", url);
            return [];
        }

        var firstAlbum = await spotifyClient.Artists.GetAlbums(artistId, new ArtistsAlbumsRequest()
        {
            IncludeGroupsParam = IncludeGroups.Album | IncludeGroups.Single | IncludeGroups.AppearsOn,
            Limit = 50
        });
        var albums = await spotifyClient.PaginateAll(firstAlbum);
        return [.. albums];
    }
}
