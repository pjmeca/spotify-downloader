using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SpotifyDownloader.Models;
using SpotifyDownloader.Services;

namespace SpotifyDownloader.Pages;

public class IndexModel(ITrackingEditorService trackingEditorService) : PageModel
{
    public TrackingInformation TrackingInformation { get; private set; } = new();
    public bool IsTrackingFileWritable { get; private set; }
    public string? AlertMessage { get; private set; }
    public bool AlertIsSuccess { get; private set; }

    [BindProperty]
    public TrackingEntryInput Entry { get; set; } = new();

    [BindProperty]
    public TrackingEntryType DeleteEntryType { get; set; }

    [BindProperty]
    public int DeleteIndex { get; set; }

    public void OnGet() => LoadPageState();

    public IActionResult OnPostSave()
    {
        var result = trackingEditorService.SaveEntry(Entry);
        LoadPageState(result);
        return Page();
    }

    public IActionResult OnPostDelete()
    {
        var result = trackingEditorService.DeleteEntry(DeleteEntryType, DeleteIndex);
        LoadPageState(result);
        return Page();
    }

    private void LoadPageState(TrackingEditorResult? result = null)
    {
        TrackingInformation = trackingEditorService.GetTrackingInformation();
        IsTrackingFileWritable = trackingEditorService.IsTrackingFileWritable();
        AlertMessage = result?.Message;
        AlertIsSuccess = result?.Success ?? false;
    }
}
