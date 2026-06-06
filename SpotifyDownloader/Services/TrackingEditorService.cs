using SpotifyDownloader.Models;

namespace SpotifyDownloader.Services;

public interface ITrackingEditorService
{
    TrackingInformation GetTrackingInformation();
    bool IsTrackingFileWritable();
    TrackingEditorResult SaveEntry(TrackingEntryInput input);
    TrackingEditorResult DeleteEntry(TrackingEntryType entryType, int index);
}

public class TrackingEditorService(ITrackingService trackingService, IFileManagementService fileManagementService) : ITrackingEditorService
{
    public TrackingInformation GetTrackingInformation() => trackingService.ReadTrackingInformation();

    public bool IsTrackingFileWritable() => trackingService.IsTrackingFileWritable();

    public TrackingEditorResult SaveEntry(TrackingEntryInput input)
    {
        var validationError = Validate(input);
        if (validationError is not null)
        {
            return new TrackingEditorResult(false, validationError);
        }

        try
        {
            var trackingInformation = trackingService.ReadTrackingInformation();
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

            trackingService.WriteTrackingInformation(trackingInformation);
            return new TrackingEditorResult(true, "Changes saved to tracking.yaml.");
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or DirectoryNotFoundException)
        {
            return new TrackingEditorResult(false, "tracking.yaml could not be saved. Check that the file exists and is mounted writable (Docker users should remove :ro from the /app/tracking.yaml mount).");
        }
    }

    public TrackingEditorResult DeleteEntry(TrackingEntryType entryType, int index)
    {
        if (index < 0)
        {
            return new TrackingEditorResult(false, "Invalid entry selected for deletion.");
        }

        try
        {
            var trackingInformation = trackingService.ReadTrackingInformation();
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

            trackingService.WriteTrackingInformation(trackingInformation);
            return new TrackingEditorResult(true, "Entry removed from tracking.yaml.");
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
}
