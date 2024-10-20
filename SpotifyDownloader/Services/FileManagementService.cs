using Microsoft.Extensions.Logging;
using SpotifyDownloader.Helpers;
using SpotifyDownloader.Models;
using SpotifyDownloader.Utils;

namespace SpotifyDownloader.Services;

public interface IFileManagementService
{
    /// <summary>
    /// If we have just upgraded from v2.0.0 or lower, move each artist and playlist to their corresponding subfolder.
    /// </summary>
    void MigrateFromOlderVersion(TrackingInformation trackingInformation);
    /// <summary>
    /// Groups tracks by albums in the file system. If an album only has one track, it won't be moved.
    /// </summary>
    void OrganizeArtists(IEnumerable<string> artistsNames);
}

public class FileManagementService(ILogger<FileManagementService> logger) : IFileManagementService
{
    public void MigrateFromOlderVersion(TrackingInformation trackingInformation)
    {
        Directory.CreateDirectory(GlobalConfiguration.ARTISTS_DIRECTORY);
        Directory.CreateDirectory(GlobalConfiguration.PLAYLISTS_DIRECTORY);

        var musicSubDirectories = Directory.GetDirectories(GlobalConfiguration.MUSIC_DIRECTORY, "*", SearchOption.TopDirectoryOnly)
            .Select(x => (FullPath: x, Name: Path.GetFileName(x)))
            .Where(x => x.FullPath != GlobalConfiguration.ARTISTS_DIRECTORY && x.FullPath != GlobalConfiguration.PLAYLISTS_DIRECTORY)
            .ToList();

        Move(trackingInformation.Artists, GlobalConfiguration.ARTISTS_DIRECTORY);
        Move(trackingInformation.Playlists, GlobalConfiguration.PLAYLISTS_DIRECTORY);

        OrganizeArtists(trackingInformation.Artists.Select(x => x.Name));

        void Move(IEnumerable<TrackingInformation.Item> items, string destinationDirectory)
        {
            if (!items.Any())
            {
                return;
            }

            var itemsToMove = items.Where(x => musicSubDirectories.Exists(y => y.Name == x.Name));
            if (!itemsToMove.Any())
            {
                return;
            }

            logger.LogInformation("Moving {num} items to \"{dir}\"...", itemsToMove.Count(), destinationDirectory);
            foreach (var item in itemsToMove.Select(x => x.Name))
            {
                MoveItem(destinationDirectory, item);
            }

            void MoveItem(string destinationDirectory, string item)
            {
                try
                {
                    var origin = musicSubDirectories.Find(x => x.Name == item);
                    if (origin != default)
                    {
                        string destination = $"{destinationDirectory}/{origin.Name}";
                        logger.LogInformation("Moving \"{origin}\" to \"{dest}\"...", origin.FullPath, destination);
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

    public void OrganizeArtists(IEnumerable<string> artistsNames)
    {
        foreach (var artist in artistsNames)
        {
            OrganizeArtist(artist);
        }

        // In case something went wrong, delete empty directories
        var emptyDirectories = Directory.GetDirectories(GlobalConfiguration.ARTISTS_DIRECTORY, "*", SearchOption.AllDirectories)
            .Where(x => Directory.GetFileSystemEntries(x).Length == 0)
            .ToList();
        foreach (var directory in emptyDirectories)
        {
            Directory.Delete(directory);
        }

        return;

        void OrganizeArtist(string artistName)
        {
            logger.LogInformation("Organizing artist \"{name}\"...", artistName);

            string artistPath = $"{GlobalConfiguration.ARTISTS_DIRECTORY}/{artistName}";
            if (!Directory.Exists(artistPath))
            {
                logger.LogInformation("Directory for artist \"{name}\" does not exist yet. Skipping.", artistName);
                return;
            }
            var albumsToOrganize = Directory.GetFiles(artistPath, "*", SearchOption.TopDirectoryOnly)
                .Select(file => Fluents.Fluent
                    .Try(() => TagLib.File.Create(file))
                    .Catch(e => logger.LogError(e, "An error occurred while creating the TagLib file for \"{file}\"." +
                        " This file will be ignored in the process of organizing the artist \"{artistName}\".", file, artistName))
                    .Execute<TagLib.File?>())
                .Where(x => x is not null)
                .GroupBy(x => x!.Tag.Album)
                .Where(x => x.Key is not null)
                .Where(x => x.Count() > 1) // Avoid singles
                .ToDictionary(x => x.Key, x => x.Select(x => Path.GetFileName(x.Name)).ToList());
            
            if (albumsToOrganize.Count == 0)
            {
                logger.LogInformation("Artist \"{name}\" is already organized.", artistName);
                return;
            }

            logger.LogInformation("{num} albums will be organized.", albumsToOrganize.Count);
            foreach (var album in albumsToOrganize)
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
                    logger.LogError(ex, "An error occurred while organizing the album {album}.", album);
                }
            }

            logger.LogInformation("Organized artist \"{name}\".", artistName);
        }
    }
}
