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

    public void OnGet() => LoadPageState();

    public IActionResult OnPostSave()
    {
        var result = trackingEditorService.SaveEntry(Entry);
        AlertMessage = result.Message;
        AlertIsSuccess = result.Success;
        return RedirectToPage();
    }

    public IActionResult OnPostDelete()
    {
        var result = trackingEditorService.DeleteEntry(DeleteEntryType, DeleteIndex);
        AlertMessage = result.Message;
        AlertIsSuccess = result.Success;
        return RedirectToPage();
    }

    private void LoadPageState()
    {
        TrackingInformation = trackingEditorService.GetTrackingInformation();
        IsTrackingFileWritable = trackingEditorService.IsTrackingFileWritable();
    }
}
