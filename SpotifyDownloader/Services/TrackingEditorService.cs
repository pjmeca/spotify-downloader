using Microsoft.Extensions.Logging;
using SpotifyDownloader.Models;
using System.Runtime.ExceptionServices;

namespace SpotifyDownloader.Services;

public interface ITrackingEditorService
{
    Task<TrackingInformation> GetTrackingInformation(CancellationToken cancellationToken = default);
    Task<bool> IsTrackingFileWritable(CancellationToken cancellationToken = default);
    Task<TrackingEditorResult> SaveEntry(TrackingEntryInput input, CancellationToken cancellationToken = default);
    Task<TrackingEditorResult> DeleteEntry(TrackingEntryType entryType, int index, CancellationToken cancellationToken = default);
    Task<TrackingEditorResult> ReorderEntries(TrackingEntryType entryType, IReadOnlyList<int> orderedIndexes, CancellationToken cancellationToken = default);
}

public class TrackingEditorService(ITrackingService trackingService, IFileManagementService fileManagementService, ILogger<TrackingEditorService> logger) : ITrackingEditorService
{
    public Task<TrackingInformation> GetTrackingInformation(CancellationToken cancellationToken = default) =>
        trackingService.ReadTrackingInformation(cancellationToken: cancellationToken);

    public Task<bool> IsTrackingFileWritable(CancellationToken cancellationToken = default) =>
        trackingService.IsTrackingFileWritable(cancellationToken: cancellationToken);

    public async Task<TrackingEditorResult> SaveEntry(TrackingEntryInput input, CancellationToken cancellationToken = default)
    {
        var validationError = Validate(input);
        if (validationError is not null)
        {
            return new TrackingEditorResult(false, validationError);
        }

        try
        {
            (TrackingEntryType EntryType, string PreviousName, string NewName, string Url)? pendingRename = null;
            return await trackingService.UpdateTrackingInformation(trackingInformation =>
                {
                    string? previousName = null;
                    bool oldNameStillInUse;
                    var newName = input.Name.Trim();
                    var url = input.Url.Trim();
                    string savedName;

                    if (input.EntryType == TrackingEntryType.Artist)
                    {
                        previousName = GetPreviousName(trackingInformation.Artists, input.Index);
                        var artist = new TrackingInformation.ArtistItem
                        {
                            Name = newName,
                            Url = url,
                            Refresh = input.Refresh
                        };
                        savedName = artist.Name;

                        Upsert(trackingInformation.Artists, input.Index, artist);
                        oldNameStillInUse = IsNameInUse(trackingInformation.Artists, previousName);
                    }
                    else
                    {
                        previousName = GetPreviousName(trackingInformation.Playlists, input.Index);
                        var playlist = new TrackingInformation.PlaylistItem
                        {
                            Name = newName,
                            Url = url,
                            Refresh = input.Refresh,
                            Mode = input.Mode
                        };
                        savedName = playlist.Name;

                        Upsert(trackingInformation.Playlists, input.Index, playlist);
                        oldNameStillInUse = IsNameInUse(trackingInformation.Playlists, previousName);
                    }

                    if (previousName is not null && !oldNameStillInUse)
                    {
                        pendingRename = (input.EntryType, previousName, savedName, url);
                    }

                    return (new TrackingEditorResult(true, "Changes saved to tracking.yaml."), true);
                },
                afterWrite: () =>
                {
                    if (pendingRename is not null)
                    {
                        fileManagementService.RenameTrackedItemDirectory(pendingRename.Value.EntryType, pendingRename.Value.PreviousName, pendingRename.Value.NewName);
                    }
                },
                handleAfterWriteException: (ex, trackingInformation) =>
                {
                    if (pendingRename is null || ex is not (UnauthorizedAccessException or IOException or DirectoryNotFoundException))
                    {
                        ExceptionDispatchInfo.Capture(ex).Throw();
                    }

                    logger.LogError(ex, "Failed to rename {entryType} directory from \"{previousName}\" to \"{newName}\" after saving tracking.yaml. Attempting to roll back tracking.yaml.",
                        pendingRename.Value.EntryType, pendingRename.Value.PreviousName, pendingRename.Value.NewName);

                    var rolledBack = RollBackTrackingName(trackingInformation, pendingRename.Value.EntryType, pendingRename.Value.NewName, pendingRename.Value.PreviousName, pendingRename.Value.Url);

                    if (!rolledBack)
                    {
                        logger.LogError("Could not roll back tracking.yaml after {entryType} directory rename failed from \"{previousName}\" to \"{newName}\".",
                            pendingRename.Value.EntryType, pendingRename.Value.PreviousName, pendingRename.Value.NewName);
                        return (new TrackingEditorResult(false,
                            "The local directory could not be renamed, and tracking.yaml could not be rolled back. Check the server logs before running the downloader again."), false);
                    }

                    logger.LogWarning("Rolled back tracking.yaml after {entryType} directory rename failed from \"{previousName}\" to \"{newName}\".",
                        pendingRename.Value.EntryType, pendingRename.Value.PreviousName, pendingRename.Value.NewName);
                    return (new TrackingEditorResult(false,
                        "The local directory could not be renamed, so tracking.yaml was rolled back. Check /music permissions and try again."), true);
                }, cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or DirectoryNotFoundException)
        {
            return new TrackingEditorResult(false, "tracking.yaml could not be saved. Check that the file exists and is mounted writable (Docker users should remove :ro from the /app/tracking.yaml mount).");
        }
    }

    public async Task<TrackingEditorResult> DeleteEntry(TrackingEntryType entryType, int index, CancellationToken cancellationToken = default)
    {
        if (index < 0)
        {
            return new TrackingEditorResult(false, "Invalid entry selected for deletion.");
        }

        try
        {
            return await trackingService.UpdateTrackingInformation(trackingInformation =>
            {
                if (entryType == TrackingEntryType.Artist)
                {
                    if (index >= trackingInformation.Artists.Count)
                    {
                        return (new TrackingEditorResult(false, "Artist not found."), false);
                    }

                    trackingInformation.Artists.RemoveAt(index);
                }
                else
                {
                    if (index >= trackingInformation.Playlists.Count)
                    {
                        return (new TrackingEditorResult(false, "Playlist not found."), false);
                    }

                    trackingInformation.Playlists.RemoveAt(index);
                }

                return (new TrackingEditorResult(true, "Entry removed from tracking.yaml."), true);
            }, cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or DirectoryNotFoundException)
        {
            return new TrackingEditorResult(false, "tracking.yaml could not be saved. Check that the file exists and is mounted writable (Docker users should remove :ro from the /app/tracking.yaml mount).");
        }
    }

    public async Task<TrackingEditorResult> ReorderEntries(TrackingEntryType entryType, IReadOnlyList<int> orderedIndexes, CancellationToken cancellationToken = default)
    {
        try
        {
            return await trackingService.UpdateTrackingInformation(trackingInformation =>
            {
                if (entryType == TrackingEntryType.Artist)
                {
                    Reorder(trackingInformation.Artists, orderedIndexes);
                }
                else
                {
                    Reorder(trackingInformation.Playlists, orderedIndexes);
                }

                return (new TrackingEditorResult(true, "Order saved to tracking.yaml."), true);
            }, cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or DirectoryNotFoundException)
        {
            return new TrackingEditorResult(false, "tracking.yaml could not be saved. Check that the file exists and is mounted writable (Docker users should remove :ro from the /app/tracking.yaml mount).");
        }
    }

    private static bool RollBackTrackingName(TrackingInformation trackingInformation, TrackingEntryType entryType, string currentName, string previousName, string url)
    {
        var items = entryType == TrackingEntryType.Artist
            ? trackingInformation.Artists.Cast<TrackingInformation.BaseItem>()
            : trackingInformation.Playlists.Cast<TrackingInformation.BaseItem>();
        var item = items.FirstOrDefault(x => x.Name == currentName && x.Url == url);
        if (item is null)
        {
            return false;
        }

        item.Name = previousName;
        return true;
    }

    private static string? GetPreviousName<T>(IList<T> items, int? index) where T : TrackingInformation.BaseItem
    {
        if (index is null)
        {
            return null;
        }

        if (index.Value < 0 || index.Value >= items.Count)
        {
            throw new IOException("The selected tracking entry no longer exists.");
        }

        return items[index.Value].Name;
    }

    private static bool IsNameInUse<T>(IEnumerable<T> items, string? name) where T : TrackingInformation.BaseItem
    {
        return name is not null && items.Any(x => x.Name == name);
    }

    private static string? Validate(TrackingEntryInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
        {
            return "Name is required.";
        }

        if (IsPathLikeName(input.Name))
        {
            return "Name must be a folder name, not a path.";
        }

        if (string.IsNullOrWhiteSpace(input.Url))
        {
            return "URL is required.";
        }

        if (!Uri.TryCreate(input.Url.Trim(), UriKind.Absolute, out _))
        {
            return "URL must be a valid absolute URI.";
        }

        return null;
    }

    private static bool IsPathLikeName(string name)
    {
        var trimmedName = name.Trim();
        return Path.IsPathRooted(trimmedName)
            || trimmedName.Contains('/')
            || trimmedName.Contains('\\')
            || trimmedName.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries).Any(x => x is "." or "..");
    }

    private static void Upsert<T>(IList<T> items, int? index, T value)
    {
        if (index is null)
        {
            items.Add(value);
            return;
        }

        if (index.Value < 0 || index.Value >= items.Count)
        {
            throw new IOException("The selected tracking entry no longer exists.");
        }

        items[index.Value] = value;
    }

    private static void Reorder<T>(IList<T> items, IReadOnlyList<int> orderedIndexes)
    {
        if (orderedIndexes.Count != items.Count || orderedIndexes.Distinct().Count() != items.Count)
        {
            throw new IOException("The submitted order does not match the current tracking entries.");
        }

        var reordered = new List<T>(items.Count);
        foreach (var index in orderedIndexes)
        {
            if (index < 0 || index >= items.Count)
            {
                throw new IOException("The submitted order does not match the current tracking entries.");
            }

            reordered.Add(items[index]);
        }

        items.Clear();
        foreach (var item in reordered)
        {
            items.Add(item);
        }
    }
}
