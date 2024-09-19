namespace SpotifyDownloader.Utils;

public static class StringUtils
{
    public static string? ValueOrNull(this string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value;
    }
}
