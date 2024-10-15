using SpotifyDownloader.Helpers;

namespace SpotifyDownloader.Utils;

public static class BannerRetriever
{
    public static async Task<string> GetBanner()
    {
        const string projectName = "spotify-downloader";
        var version = GlobalConfiguration.VERSION;

        var client = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://banners.pjmeca.com/?projectName={projectName}&version={version}");
        request.Headers.Add("accept", "text/plain");
        var response = await client.SendAsync(request);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadAsStringAsync();
        }
        else
        {
            // If we cannot retrieve the dynamic banner, use the static local copy
            return await File.ReadAllTextAsync("./banner");
        }
    }
}
