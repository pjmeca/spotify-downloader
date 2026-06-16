using Microsoft.Extensions.Logging;
using SpotifyDownloader.Models;
using SpotifyDownloader.Utils;

namespace SpotifyDownloader.Services;

public interface ITrackingEditorService
{
    Task<TrackingInformation> GetTrackingInformation(CancellationToken cancellationToken = default);
    Task<bool> IsTrackingFileWritable(CancellationToken cancellationToken = default);
    Task<TrackingEditorResult> SaveEntry(TrackingEntryInput input, CancellationToken cancellationToken = default);
    Task<TrackingEditorResult> DeleteEntry(TrackingEntryType entryType, int index, string? originalName, string? originalUrl, CancellationToken cancellationToken = default);
    Task<TrackingEditorResult> ReorderEntries(TrackingEntryType entryType, IReadOnlyList<int> orderedIndexes, IReadOnlyList<TrackingReorderEntryInput> orderedEntries, CancellationToken cancellationToken = default);
}

public class TrackingEditorService(ITrackingService trackingService, IFileManagementService fileManagementService,
    IFileOperationCoordinator fileOperationCoordinator, ILogger<TrackingEditorService> logger) : ITrackingEditorService
{
    private const string DownloadInProgressMessage = "A download is currently in progress. Tracking changes are disabled until it finishes.";

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
            var lockResult = await fileOperationCoordinator.TryRunWithExclusiveMusicAccess(async () =>
            {
                var result = await trackingService.UpdateTrackingInformation(trackingInformation =>
                {
                    string? previousName = null;
                    bool oldNameStillInUse;
                    var newName = input.Name.Trim();
                    var url = input.Url.Trim();
                    string savedName;

                    if (input.EntryType == TrackingEntryType.Artist)
                    {
                        previousName = GetPreviousName(trackingInformation.Artists, input);
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
                        previousName = GetPreviousName(trackingInformation.Playlists, input);
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
                }, cancellationToken: cancellationToken);

                return pendingRename is null
                    ? result
                    : await RenameTrackedItemDirectoryWithRollback(pendingRename.Value, result, cancellationToken);
            });

            return lockResult.Acquired
                ? lockResult.Result!
                : new TrackingEditorResult(false, DownloadInProgressMessage);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or DirectoryNotFoundException)
        {
            return new TrackingEditorResult(false, "tracking.yaml could not be saved. Check that the file exists and is mounted writable (Docker users should remove :ro from the /app/tracking.yaml mount).");
        }
    }

    public async Task<TrackingEditorResult> DeleteEntry(TrackingEntryType entryType, int index, string? originalName, string? originalUrl, CancellationToken cancellationToken = default)
    {
        if (index < 0)
        {
            return new TrackingEditorResult(false, "Invalid entry selected for deletion.");
        }

        try
        {
            var lockResult = await fileOperationCoordinator.TryRunWithExclusiveMusicAccess(() => trackingService.UpdateTrackingInformation(trackingInformation =>
            {
                return entryType == TrackingEntryType.Artist
                    ? DeleteFrom(trackingInformation.Artists, index, originalName, originalUrl, "Artist")
                    : DeleteFrom(trackingInformation.Playlists, index, originalName, originalUrl, "Playlist");
            }, cancellationToken: cancellationToken));

            return lockResult.Acquired
                ? lockResult.Result!
                : new TrackingEditorResult(false, DownloadInProgressMessage);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or DirectoryNotFoundException)
        {
            return new TrackingEditorResult(false, "tracking.yaml could not be saved. Check that the file exists and is mounted writable (Docker users should remove :ro from the /app/tracking.yaml mount).");
        }
    }

    public async Task<TrackingEditorResult> ReorderEntries(TrackingEntryType entryType, IReadOnlyList<int> orderedIndexes, IReadOnlyList<TrackingReorderEntryInput> orderedEntries, CancellationToken cancellationToken = default)
    {
        try
        {
            var lockResult = await fileOperationCoordinator.TryRunWithExclusiveMusicAccess(() => trackingService.UpdateTrackingInformation(trackingInformation =>
            {
                var reorderError = entryType == TrackingEntryType.Artist
                    ? Reorder(trackingInformation.Artists, orderedIndexes, orderedEntries)
                    : Reorder(trackingInformation.Playlists, orderedIndexes, orderedEntries);

                if (reorderError is not null)
                {
                    return (new TrackingEditorResult(false, reorderError), false);
                }

                return (new TrackingEditorResult(true, "Order saved to tracking.yaml."), true);
            }, cancellationToken: cancellationToken));

            return lockResult.Acquired
                ? lockResult.Result!
                : new TrackingEditorResult(false, DownloadInProgressMessage);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or DirectoryNotFoundException)
        {
            return new TrackingEditorResult(false, "tracking.yaml could not be saved. Check that the file exists and is mounted writable (Docker users should remove :ro from the /app/tracking.yaml mount).");
        }
    }

    private static (TrackingEditorResult Result, bool ShouldWrite) DeleteFrom<T>(IList<T> items, int index, string? originalName, string? originalUrl, string entryLabel)
        where T : TrackingInformation.BaseItem
    {
        if (index >= items.Count)
        {
            return (new TrackingEditorResult(false, $"{entryLabel} not found."), false);
        }

        if (!MatchesOriginalIdentity(items[index], originalName, originalUrl))
        {
            return (new TrackingEditorResult(false, $"The selected {entryLabel.ToLowerInvariant()} changed since this page was loaded. Reload the page and try again."), false);
        }

        items.RemoveAt(index);
        return (new TrackingEditorResult(true, "Entry removed from tracking.yaml."), true);
    }

    private async Task<TrackingEditorResult> RenameTrackedItemDirectoryWithRollback(
        (TrackingEntryType EntryType, string PreviousName, string NewName, string Url) rename,
        TrackingEditorResult successResult,
        CancellationToken cancellationToken)
    {
        try
        {
            fileManagementService.RenameTrackedItemDirectory(rename.EntryType, rename.PreviousName, rename.NewName);
            return successResult;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or DirectoryNotFoundException)
        {
            logger.LogError(ex, "Failed to rename {entryType} directory from \"{previousName}\" to \"{newName}\" after saving tracking.yaml. Attempting to roll back tracking.yaml.",
                rename.EntryType, rename.PreviousName, rename.NewName);

            var rollbackResult = await trackingService.UpdateTrackingInformation(trackingInformation =>
            {
                var rolledBack = RollBackTrackingName(trackingInformation, rename.EntryType, rename.NewName, rename.PreviousName, rename.Url);
                return (rolledBack, rolledBack);
            }, cancellationToken: cancellationToken);

            if (!rollbackResult)
            {
                logger.LogError("Could not roll back tracking.yaml after {entryType} directory rename failed from \"{previousName}\" to \"{newName}\".",
                    rename.EntryType, rename.PreviousName, rename.NewName);
                return new TrackingEditorResult(false,
                    "The local directory could not be renamed, and tracking.yaml could not be rolled back. Check the server logs before running the downloader again.");
            }

            logger.LogWarning("Rolled back tracking.yaml after {entryType} directory rename failed from \"{previousName}\" to \"{newName}\".",
                rename.EntryType, rename.PreviousName, rename.NewName);
            return new TrackingEditorResult(false,
                "The local directory could not be renamed, so tracking.yaml was rolled back. Check /music permissions and try again.");
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

    private static string? GetPreviousName<T>(IList<T> items, TrackingEntryInput input) where T : TrackingInformation.BaseItem
    {
        if (input.Index is null)
        {
            return null;
        }

        if (input.Index.Value < 0 || input.Index.Value >= items.Count)
        {
            throw new IOException("The selected tracking entry no longer exists.");
        }

        var item = items[input.Index.Value];
        if (!MatchesOriginalEditableFields(item, input))
        {
            throw new IOException("The selected tracking entry changed since this editor was opened. Reload the page and try again.");
        }

        return item.Name;
    }

    private static bool MatchesOriginalIdentity(TrackingInformation.BaseItem item, string? originalName, string? originalUrl) =>
        item.Name == originalName && item.Url == originalUrl;

    private static bool MatchesOriginalEditableFields(TrackingInformation.BaseItem item, TrackingEntryInput input)
    {
        if (!MatchesOriginalIdentity(item, input.OriginalName, input.OriginalUrl) || item.Refresh != input.OriginalRefresh)
        {
            return false;
        }

        return item is not TrackingInformation.PlaylistItem playlist || playlist.Mode == input.OriginalMode;
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

        if (!Uri.TryCreate(input.Url.Trim(), UriKind.Absolute, out var uri))
        {
            return "URL must be a valid absolute URI.";
        }

        if (uri.Scheme is not "http" and not "https")
        {
            return "URL must use http or https.";
        }

        if (!IsSpotifyTrackingUrl(uri, input.EntryType))
        {
            return input.EntryType == TrackingEntryType.Artist
                ? "Artist URL must be a Spotify artist URL."
                : "Playlist URL must be a Spotify playlist URL.";
        }

        return null;
    }

    private static bool IsSpotifyTrackingUrl(Uri uri, TrackingEntryType entryType)
    {
        var expectedSegment = entryType == TrackingEntryType.Artist ? "artist" : "playlist";
        return SpotifyUrlUtils.GetResourceId(uri, expectedSegment) is not null;
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

    private static string? Reorder<T>(IList<T> items, IReadOnlyList<int> orderedIndexes, IReadOnlyList<TrackingReorderEntryInput> orderedEntries)
        where T : TrackingInformation.BaseItem
    {
        if (orderedIndexes.Count != items.Count || orderedEntries.Count != items.Count || orderedIndexes.Distinct().Count() != items.Count)
        {
            return "The submitted order does not match the current tracking entries.";
        }

        var reordered = new List<T>(items.Count);
        for (var i = 0; i < orderedIndexes.Count; i++)
        {
            var index = orderedIndexes[i];
            var entry = orderedEntries[i];
            if (index < 0 || index >= items.Count)
            {
                return "The submitted order does not match the current tracking entries.";
            }

            if (entry.Index != index || !MatchesOriginalIdentity(items[index], entry.Name, entry.Url))
            {
                return "The tracking entries changed since this page was loaded. Reload the page and try again.";
            }

            reordered.Add(items[index]);
        }

        items.Clear();
        foreach (var item in reordered)
        {
            items.Add(item);
        }

        return null;
    }
}
