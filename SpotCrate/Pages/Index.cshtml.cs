using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json;
using SpotCrate.Models;
using SpotCrate.Services;

namespace SpotCrate.Pages;

public class IndexModel(TrackingEditorService trackingEditorService) : PageModel
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

    [BindProperty]
    public string OrderedEntries { get; set; } = string.Empty;

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
        var orderedEntries = ParseOrderedEntries();
        var result = await trackingEditorService.ReorderEntries(ReorderEntryType, orderedIndexes, orderedEntries, cancellationToken);
        SetErrorAlert(result);
        return RedirectToPage();
    }

    public static string GetPlaylistModeLabel(PlaylistDownloadMode mode) => mode switch
    {
        PlaylistDownloadMode.Full => "Mirrors Spotify",
        _ => "Downloads new songs only"
    };

    public static bool IsClickableTrackingUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";

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

    private IReadOnlyList<TrackingReorderEntryInput> ParseOrderedEntries()
    {
        try
        {
            return JsonConvert.DeserializeObject<List<TrackingReorderEntryInput>>(OrderedEntries) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
