using Microsoft.Extensions.Logging;
using SpotifyDownloader.Helpers;
using SpotifyDownloader.Models;
using SpotifyDownloader.Utils;

namespace SpotifyDownloader.Services;

public interface IFileManagmentService
{
    /// <summary>
    /// If we have just upgraded from v2.0.0 or lower, move each artist and playlist to their corresponding subfolder.
    /// </summary>
    void MigrateFromOlderVersion(TrackingInformation trackingInformation);
    /// <summary>
    /// Groups tracks by albums in the file system. If an album only has one track, it won't be arranged.
    /// </summary>
    void ArrangeArtists(IEnumerable<string> artistsNames);
}

public class FileManagmentService(ILogger<FileManagmentService> logger) : IFileManagmentService
{
    public const string ArtistsDirectory = $"{GlobalConfiguration.MUSIC_DIRECTORY}/Artists";
    public const string PlaylistsDirectory = $"{GlobalConfiguration.MUSIC_DIRECTORY}/Playlists";

    public void MigrateFromOlderVersion(TrackingInformation trackingInformation)
    {
        Directory.CreateDirectory(ArtistsDirectory);
        Directory.CreateDirectory(PlaylistsDirectory);

        var musicSubDirectories = Directory.GetDirectories(GlobalConfiguration.MUSIC_DIRECTORY, "*", SearchOption.TopDirectoryOnly)
            .Select(x => (FullPath: x, Name: Path.GetFileName(x)))
            .Where(x => x.FullPath != ArtistsDirectory && x.FullPath != PlaylistsDirectory)
            .ToList();

        Move(trackingInformation.Artists, ArtistsDirectory);
        Move(trackingInformation.Playlists, PlaylistsDirectory);

        ArrangeArtists(trackingInformation.Artists.Select(x => x.Name));

        void Move(IEnumerable<TrackingInformation.Item> items, string destinationDirectory)
        {
            if (!items.Any())
            {
                return;
            }

            logger.LogInformation("Moving {num} items to \"{dir}\"...", items.Count(), destinationDirectory);
            foreach (var item in items.Select(x => x.Name))
            {
                try
                {
                    var origin = musicSubDirectories.Find(x => x.Name == item);
                    if (origin != default)
                    {
                        string destination = $"{destinationDirectory}/{origin.Name}";
                        logger.LogInformation("Moving {origin} to {dest}...", origin.FullPath, destination);
                        DirectoryUtils.MoveAndMerge(origin.FullPath, destination);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred while moving {name}.", item);
                }
            }
        }
    }

    public void ArrangeArtists(IEnumerable<string> artistsNames)
    {
        foreach (var artist in artistsNames)
        {
            ArrangeArtist(artist);
        }

        void ArrangeArtist(string artistName)
        {
            logger.LogInformation("Arranging artist {name}...", artistName);

            string artistPath = $"{ArtistsDirectory}/{artistName}";
            var albumsToArrange = Directory.GetFiles(artistPath, "*", SearchOption.TopDirectoryOnly)
                .Select(x => TagLib.File.Create(x))
                .GroupBy(x => x.Tag.Album)
                .Where(x => x.Count() > 1) // Avoid singles
                .ToDictionary(x => x.Key, x => x.Select(x => Path.GetFileName(x.Name)).ToList());
            
            logger.LogInformation("{num} albums will be arranged.", albumsToArrange.Count);
            foreach (var album in albumsToArrange)
            {
                try
                {
                    string albumName = album.Key.ToValidPathString();
                    string albumDirectory = $"{artistPath}/{albumName}";
                    var tracks = album.Value;

                    Directory.CreateDirectory(albumDirectory);
                    tracks.ForEach(x => File.Move($"{artistPath}/{x}", $"{albumDirectory}/{x}"));
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred while arranging the album {album}.", album);
                }
            }

            logger.LogInformation("Arranged artist {name}.", artistName);
        }
    }
}
