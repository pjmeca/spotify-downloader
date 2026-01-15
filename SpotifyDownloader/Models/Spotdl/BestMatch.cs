namespace SpotifyDownloader.Models.Spotdl;

public sealed class BestMatch
{
    public SpotdlResult Result { get; init; } = new();
    public double Score { get; init; }
}