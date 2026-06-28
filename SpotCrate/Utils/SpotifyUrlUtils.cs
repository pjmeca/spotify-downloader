namespace SpotCrate.Utils;

public static class SpotifyUrlUtils
{
    public static string? GetResourceId(string url, string resourceType)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            ? GetResourceId(uri, resourceType)
            : null;
    }

    public static string? GetResourceId(Uri uri, string resourceType)
    {
        if (!uri.Host.Equals("spotify.com", StringComparison.OrdinalIgnoreCase)
            && !uri.Host.EndsWith(".spotify.com", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var pathSegments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var segmentIndex = Array.FindIndex(pathSegments, x => x.Equals(resourceType, StringComparison.OrdinalIgnoreCase));
        return segmentIndex >= 0
            && segmentIndex == pathSegments.Length - 2
            && !string.IsNullOrWhiteSpace(pathSegments[^1])
            ? pathSegments[^1]
            : null;
    }
}
