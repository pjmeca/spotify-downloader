using SpotifyDownloader.Models;

namespace SpotifyDownloader.Services;

public interface ITrackingEditorService
{
    Task<TrackingInformation> GetTrackingInformation(CancellationToken cancellationToken = default);
    Task<bool> IsTrackingFileWritable(CancellationToken cancellationToken = default);
    Task<TrackingEditorResult> SaveEntry(TrackingEntryInput input, CancellationToken cancellationToken = default);
    Task<TrackingEditorResult> DeleteEntry(TrackingEntryType entryType, int index, CancellationToken cancellationToken = default);
    Task<TrackingEditorResult> ReorderEntries(TrackingEntryType entryType, IReadOnlyList<int> orderedIndexes, CancellationToken cancellationToken = default);
}

public class TrackingEditorService(ITrackingService trackingService, IFileManagementService fileManagementService) : ITrackingEditorService
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
            var trackingInformation = await trackingService.ReadTrackingInformation(cancellationToken: cancellationToken);
            string? previousName = null;
            bool oldNameStillInUse;

            if (input.EntryType == TrackingEntryType.Artist)
            {
                previousName = GetPreviousName(trackingInformation.Artists, input.Index);
                var artist = new TrackingInformation.ArtistItem
                {
                    Name = input.Name.Trim(),
                    Url = input.Url.Trim(),
                    Refresh = input.Refresh
                };

                Upsert(trackingInformation.Artists, input.Index, artist);
                oldNameStillInUse = IsNameInUse(trackingInformation.Artists, previousName);
            }
            else
            {
                previousName = GetPreviousName(trackingInformation.Playlists, input.Index);
                var playlist = new TrackingInformation.PlaylistItem
                {
                    Name = input.Name.Trim(),
                    Url = input.Url.Trim(),
                    Refresh = input.Refresh,
                    Mode = input.Mode
                };

                Upsert(trackingInformation.Playlists, input.Index, playlist);
                oldNameStillInUse = IsNameInUse(trackingInformation.Playlists, previousName);
            }

            if (previousName is not null && !oldNameStillInUse)
            {
                fileManagementService.RenameTrackedItemDirectory(input.EntryType, previousName, input.Name);
            }

            await trackingService.WriteTrackingInformation(trackingInformation, cancellationToken: cancellationToken);
            return new TrackingEditorResult(true, "Changes saved to tracking.yaml.");
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
            var trackingInformation = await trackingService.ReadTrackingInformation(cancellationToken: cancellationToken);
            if (entryType == TrackingEntryType.Artist)
            {
                if (index >= trackingInformation.Artists.Count)
                {
                    return new TrackingEditorResult(false, "Artist not found.");
                }

                trackingInformation.Artists.RemoveAt(index);
            }
            else
            {
                if (index >= trackingInformation.Playlists.Count)
                {
                    return new TrackingEditorResult(false, "Playlist not found.");
                }

                trackingInformation.Playlists.RemoveAt(index);
            }

            await trackingService.WriteTrackingInformation(trackingInformation, cancellationToken: cancellationToken);
            return new TrackingEditorResult(true, "Entry removed from tracking.yaml.");
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
            var trackingInformation = await trackingService.ReadTrackingInformation(cancellationToken: cancellationToken);
            if (entryType == TrackingEntryType.Artist)
            {
                Reorder(trackingInformation.Artists, orderedIndexes);
            }
            else
            {
                Reorder(trackingInformation.Playlists, orderedIndexes);
            }

            await trackingService.WriteTrackingInformation(trackingInformation, cancellationToken: cancellationToken);
            return new TrackingEditorResult(true, "Order saved to tracking.yaml.");
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or DirectoryNotFoundException)
        {
            return new TrackingEditorResult(false, "tracking.yaml could not be saved. Check that the file exists and is mounted writable (Docker users should remove :ro from the /app/tracking.yaml mount).");
        }
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
