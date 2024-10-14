namespace SpotifyDownloader.Utils;

public static class ExceptionUtils
{
    public static string ThrowExceptionIfNullOrWhiteSpace(this string? value, string? propertyName = null, bool throwInDevelopment = true)
    {
        if (!throwInDevelopment && Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
        {
            return value!;
        }

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
