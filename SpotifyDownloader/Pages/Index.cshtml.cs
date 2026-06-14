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
    public string? DeleteOriginalName { get; set; }

    [BindProperty]
    public string? DeleteOriginalUrl { get; set; }

    [BindProperty]
    public TrackingEntryType ReorderEntryType { get; set; }

    [BindProperty]
    public string OrderedIndexes { get; set; } = string.Empty;

    public async Task OnGetAsync(CancellationToken cancellationToken) => await LoadPageState(cancellationToken);

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        var result = await trackingEditorService.SaveEntry(Entry, cancellationToken);
        SetErrorAlert(result);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(CancellationToken cancellationToken)
    {
        var result = await trackingEditorService.DeleteEntry(DeleteEntryType, DeleteIndex, DeleteOriginalName, DeleteOriginalUrl, cancellationToken);
        SetErrorAlert(result);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostReorderAsync(CancellationToken cancellationToken)
    {
        var orderedIndexes = OrderedIndexes
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, out var index) ? index : -1)
            .ToList();
        var result = await trackingEditorService.ReorderEntries(ReorderEntryType, orderedIndexes, cancellationToken);
        SetErrorAlert(result);
        return RedirectToPage();
    }

    public static string GetPlaylistModeLabel(PlaylistDownloadMode mode) => mode switch
    {
        PlaylistDownloadMode.Full => "Mirrors Spotify",
        _ => "Downloads new songs only"
    };

    private async Task LoadPageState(CancellationToken cancellationToken)
    {
        TrackingInformation = await trackingEditorService.GetTrackingInformation(cancellationToken);
        IsTrackingFileWritable = await trackingEditorService.IsTrackingFileWritable(cancellationToken);
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
