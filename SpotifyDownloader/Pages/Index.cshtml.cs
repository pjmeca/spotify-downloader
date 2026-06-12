using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SpotifyDownloader.Models;
using SpotifyDownloader.Services;

namespace SpotifyDownloader.Pages;

public class IndexModel(ITrackingEditorService trackingEditorService) : PageModel
{
    public TrackingInformation TrackingInformation { get; private set; } = new();
    public bool IsTrackingFileWritable { get; private set; }
    [TempData]
    public string? AlertMessage { get; set; }

    [TempData]
    public bool AlertIsSuccess { get; set; }

    [BindProperty]
    public TrackingEntryInput Entry { get; set; } = new();

    [BindProperty]
    public TrackingEntryType DeleteEntryType { get; set; }

    [BindProperty]
    public int DeleteIndex { get; set; }

    [BindProperty]
    public TrackingEntryType ReorderEntryType { get; set; }

    [BindProperty]
    public string OrderedIndexes { get; set; } = string.Empty;

    public void OnGet() => LoadPageState();

    public IActionResult OnPostSave()
    {
        var result = trackingEditorService.SaveEntry(Entry);
        SetErrorAlert(result);
        return RedirectToPage();
    }

    public IActionResult OnPostDelete()
    {
        var result = trackingEditorService.DeleteEntry(DeleteEntryType, DeleteIndex);
        SetErrorAlert(result);
        return RedirectToPage();
    }

    public IActionResult OnPostReorder()
    {
        var orderedIndexes = OrderedIndexes
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, out var index) ? index : -1)
            .ToList();
        var result = trackingEditorService.ReorderEntries(ReorderEntryType, orderedIndexes);
        SetErrorAlert(result);
        return RedirectToPage();
    }

    public static string GetPlaylistModeLabel(PlaylistDownloadMode mode) => mode switch
    {
        PlaylistDownloadMode.Full => "Mirrors Spotify",
        _ => "Downloads new songs only"
    };

    private void LoadPageState()
    {
        TrackingInformation = trackingEditorService.GetTrackingInformation();
        IsTrackingFileWritable = trackingEditorService.IsTrackingFileWritable();
    }

    private void SetErrorAlert(TrackingEditorResult result)
    {
        if (result.Success)
        {
            return;
        }

        AlertMessage = result.Message;
        AlertIsSuccess = false;
    }
}
