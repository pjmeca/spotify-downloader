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

    public static string ToValidPathString(this string? value)
    {
        ArgumentNullException.ThrowIfNull(value);

        foreach (var c in Path.GetInvalidPathChars())
        {
            value = value.Replace(char.ToString(c), "");
        }
        return value;
    }
}
