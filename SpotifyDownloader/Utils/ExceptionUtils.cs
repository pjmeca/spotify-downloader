namespace SpotifyDownloader.Utils;

public static class ExceptionUtils
{
    public static string ThrowExceptionIfNullOrWhiteSpace(this string? value, string? propertyName = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (propertyName is not null)
            {
                throw new ArgumentNullException(propertyName, $"{propertyName} must not be null or empty");
            }
            else
            {
                throw new ArgumentNullException(message: "The provided string is null, empty or whitespace", null);
            }
        }

        return value;
    }

}
