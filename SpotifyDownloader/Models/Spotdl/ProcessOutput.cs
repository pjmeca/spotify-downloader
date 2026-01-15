namespace SpotifyDownloader.Models.Spotdl;

public sealed class ProcessOutput
{
    public List<string> StandardOutput { get; init; } = [];
    public List<string> StandardError { get; init; } = [];
}