namespace SpotifyDownloader.Models;

public class SpotdlException : Exception
{
    public SpotdlException(string message)
        : base(message)
    {
    }

    public SpotdlException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
