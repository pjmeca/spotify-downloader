using SpotCrate.Helpers;

namespace SpotCrate.Utils;

public static class BannerRetriever
{
    public static async Task<string> GetBanner()
    {
        const string projectName = "spotcrate";
        var version = GlobalConfiguration.CurrentVersion;

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
