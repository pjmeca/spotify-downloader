using System.ComponentModel.DataAnnotations;

namespace SpotifyDownloader.Models;

public enum TrackingEntryType
{
    Artist,
    Playlist
}

public class TrackingEntryInput
{
    public TrackingEntryType EntryType { get; set; }
    public int? Index { get; set; }
    public string? OriginalName { get; set; }
    public string? OriginalUrl { get; set; }

    [Required(ErrorMessage = "Name is required.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "URL is required.")]
    public string Url { get; set; } = string.Empty;

    public bool Refresh { get; set; } = true;
    public PlaylistDownloadMode Mode { get; set; } = PlaylistDownloadMode.Full;
}

public class TrackingReorderEntryInput
{
    public int Index { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

public record TrackingEditorResult(bool Success, string Message);
