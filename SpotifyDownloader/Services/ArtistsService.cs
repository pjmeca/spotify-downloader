using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SpotifyAPI.Web;
using SpotifyDownloader.Data;
using SpotifyDownloader.Helpers;
using SpotifyDownloader.Utils;
using static Fluents.Fluent;

namespace SpotifyDownloader.Services;

public interface IArtistsService
{
    Task<(string[] localTracks, string[] localAlbums)> GetLocalArtistInfo(string artistName);

    Task<SimpleAlbum[]> GetRemoteArtistInfo(string url);

    Task UpdateLocalArtistsInfo();
}

public class ArtistsService(ILogger<ArtistsService> logger, ISpotifyClientWrapper spotifyClient, ApplicationDbContext _applicationDbContext) : IArtistsService
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
                .Select(x => Try(() => TagLib.File.Create(x).Tag.Album).Ignore().Execute<string>())
                .Where(x => x is not null)
                .Distinct()
                .ToArray();
            var dbAlbums = await _applicationDbContext.Albums
                .Where(x => x.Artist.Name == artistName.ToValidPathString())
                .Select(x => x.Name)
                .ToListAsync();

            localAlbums = localAlbums.Concat(dbAlbums).Distinct().ToArray();
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

        var albums = await spotifyClient.GetAllAlbums(artistId);
        return [.. albums];
    }

    public async Task UpdateLocalArtistsInfo()
    {
        var localArtists = Directory.GetDirectories(GlobalConfiguration.ARTISTS_DIRECTORY, "*", SearchOption.TopDirectoryOnly)
            .Select(x => Path.GetFileName(x))
            .ToList();
        var dbArtists = await _applicationDbContext.Artists.ToListAsync();

        // 1. Create new artists
        var artistsToCreate = localArtists
            .Where(x => !dbArtists.Select(x => x.Name).Contains(x))
            .Select(x => new Artist()
            {
                Name = x
            })
            .ToList();
        await _applicationDbContext.Artists.AddRangeAsync(artistsToCreate);

        // 2. Delete artists
        var artistsToDelete = dbArtists.Where(x => !localArtists.Contains(x.Name)).ToList();
        _applicationDbContext.Artists.RemoveRange(artistsToDelete);

        // 3. Update DB
        await _applicationDbContext.SaveChangesAsync();
        dbArtists = await _applicationDbContext.Artists
            .Include(x => x.Albums)
            .ToListAsync();

        // 4. Create new albums
        foreach (var artist in dbArtists)
        {
            var dbAlbums = dbArtists
                .Where(x => x.Id == artist.Id)
                .SelectMany(x => x.Albums.Select(x => x.Name)).ToList();

            var artistPath = $"{GlobalConfiguration.ARTISTS_DIRECTORY}/{artist.Name.ToValidPathString()}";
            if (!Directory.Exists(artistPath))
            {
                continue;
            }

            var localAlbums = Directory
                .GetFiles(artistPath, "*", SearchOption.AllDirectories)
                .Select(x => Try(() => TagLib.File.Create(x).Tag.Album).Ignore().Execute<string>())
                .Where(x => x is not null)
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
