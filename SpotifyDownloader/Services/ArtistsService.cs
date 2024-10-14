using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SpotifyAPI.Web;
using SpotifyDownloader.Data;
using SpotifyDownloader.Helpers;
using SpotifyDownloader.Utils;
using static SpotifyAPI.Web.ArtistsAlbumsRequest;

namespace SpotifyDownloader.Services;

public interface IArtistsService
{
    Task<(string[] localTracks, string[] localAlbums)> GetLocalArtistInfo(string artistName);

    Task<SimpleAlbum[]> GetRemoteArtistInfo(string url);

    Task UpdateLocalArtistsInfo();
}

public class ArtistsService(ILogger<ArtistsService> logger, SpotifyClient spotifyClient, ApplicationDbContext _applicationDbContext) : IArtistsService
{
    public async Task<(string[] localTracks, string[] localAlbums)> GetLocalArtistInfo(string artistName)
    {
        var itemDirectory = $"{GlobalConfiguration.ARTISTS_DIRECTORY}/{artistName.ToValidPathString()}";

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
            var dbAlbums = await _applicationDbContext.Albums
                .Where(x => x.Artist.Name == artistName.ToValidPathString())
                .Select(x => x.Name)
                .ToListAsync();
            localAlbums = [..localAlbums, ..dbAlbums];
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

    public async Task UpdateLocalArtistsInfo()
    {
        var localArtists = Directory.GetDirectories(GlobalConfiguration.ARTISTS_DIRECTORY, "*", SearchOption.TopDirectoryOnly);
        var dbArtists = await _applicationDbContext.Artists.ToListAsync();

        // 1. Create new artists
        var artistsToCreate = localArtists
            .Where(x => !dbArtists.Select(x => x.Name).Contains(x))
            .Select(x => new Artist()
            {
                Name = x
            }).ToList();
        await _applicationDbContext.Artists.AddRangeAsync(artistsToCreate);

        // 2. Delete artists
        var artistsToDelete = dbArtists.Where(x => !localArtists.Contains(x.Name)).ToList();
        _applicationDbContext.Artists.RemoveRange(artistsToDelete);

        // 3. Update DB
        await _applicationDbContext.SaveChangesAsync();
        dbArtists = await _applicationDbContext.Artists.ToListAsync();

        // 4. Create new albums
        var dbAlbums = await _applicationDbContext.Albums.Select(x => x.Name).ToListAsync();
        foreach (var artist in dbArtists)
        {
            if (!Directory.Exists(artist.Name.ToValidPathString()))
            {
                continue;
            }

            var localAlbums = Directory
                .GetFiles($"{GlobalConfiguration.ARTISTS_DIRECTORY}/{artist.Name.ToValidPathString()}", "*", SearchOption.AllDirectories)
                .Select(x => TagLib.File.Create(x).Tag.Album)
                .Distinct();
            var albumsToCreate = localAlbums
                .Where(x => !dbAlbums.Contains(x))
                .Select(x => new Album()
                {
                    Name = x
                }).ToList();
            artist.Albums.AddRange(albumsToCreate);
        }
        _applicationDbContext.Artists.UpdateRange(dbArtists);
        await _applicationDbContext.SaveChangesAsync();
    }
}
