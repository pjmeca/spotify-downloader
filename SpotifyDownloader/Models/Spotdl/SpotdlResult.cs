namespace SpotifyDownloader.Models.Spotdl;

public sealed class SpotdlResult
{
    public string Source { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public bool Verified { get; init; }
    public string Name { get; init; } = string.Empty;
    public double Duration { get; init; }
    public string Author { get; init; } = string.Empty;
    public string ResultId { get; init; } = string.Empty;
    public bool IsrcSearch { get; init; }
    public string SearchQuery { get; init; } = string.Empty;
    public IReadOnlyList<string>? Artists { get; init; }
    public long? Views { get; init; }
    public bool? Explicit { get; init; }
    public string? Album { get; init; }
}