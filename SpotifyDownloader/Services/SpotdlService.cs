using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using SpotifyAPI.Web;
using SpotifyDownloader.Helpers;
using SpotifyDownloader.Models;
using SpotifyDownloader.Models.Spotdl;
using SpotifyDownloader.Utils;
using TagLibByteVector = TagLib.ByteVector;
using TagLibFile = TagLib.File;
using TagLibPicture = TagLib.Picture;
using TagLibPictureType = TagLib.PictureType;

namespace SpotifyDownloader.Services;

public interface ISpotdlService
{
    /// <summary>
    /// Downloads a track using Spotify metadata.
    /// </summary>
    Task DownloadTrack(string outputDirectory, FullTrack track);

    /// <summary>
    /// Downloads a track using album-scoped Spotify metadata.
    /// </summary>
    Task DownloadTrack(string outputDirectory, SimpleTrack track, SimpleAlbum album);

    /// <summary>
    /// Downloads a track by resolving its Spotify URL.
    /// </summary>
    Task DownloadTrackByUrl(string outputDirectory, string trackUrl);

    /// <summary>
    /// Downloads all tracks from a Spotify album.
    /// </summary>
    Task DownloadAlbum(string outputDirectory, SimpleAlbum album);
}

public class SpotdlService(
    ILogger<SpotdlService> logger,
    GlobalConfiguration configuration,
    ISpotifyClientWrapper spotifyClient
) : ISpotdlService
{
    private static readonly HttpClient HttpClient = new();
    private static readonly TimeSpan SearchTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(10);

    /// <inheritdoc />
    public async Task DownloadTrack(string outputDirectory, FullTrack track)
    {
        var spotdlTrack = SpotdlTrack.FromFullTrack(track);
        await DownloadTrackInternal(outputDirectory, spotdlTrack);
    }

    /// <inheritdoc />
    public async Task DownloadTrack(string outputDirectory, SimpleTrack track, SimpleAlbum album)
    {
        var spotdlTrack = SpotdlTrack.FromSimpleTrack(track, album);
        await DownloadTrackInternal(outputDirectory, spotdlTrack);
    }

    /// <inheritdoc />
    public async Task DownloadTrackByUrl(string outputDirectory, string trackUrl)
    {
        if (!TryParseSpotifyId(trackUrl, "track", out var trackId))
        {
            throw new ArgumentException($"Invalid Spotify track URL: {trackUrl}", nameof(trackUrl));
        }

        var track = await spotifyClient.GetTrack(trackId);
        var spotdlTrack = SpotdlTrack.FromFullTrack(track);
        await DownloadTrackInternal(outputDirectory, spotdlTrack);
    }

    /// <inheritdoc />
    public async Task DownloadAlbum(string outputDirectory, SimpleAlbum album)
    {
        Directory.CreateDirectory(outputDirectory);

        var albumTracks = await spotifyClient.GetAllAlbumTracks(album.Id);
        foreach (var track in albumTracks)
        {
            await DownloadTrack(outputDirectory, track, album);
        }
    }

    /// <summary>
    /// Resolves, downloads, and tags a single track.
    /// </summary>
    private async Task DownloadTrackInternal(string outputDirectory, SpotdlTrack track)
    {
        Directory.CreateDirectory(outputDirectory);

        var outputFileName = $"{string.Join(", ", track.Artists)} - {track.Name}".ToValidPathString();
        var existingFiles = Directory.GetFiles(outputDirectory, $"{outputFileName}.*");
        if (existingFiles.Length > 0)
        {
            logger.LogInformation("The track \"{name}\" already exists. Skipping...", outputFileName);
            return;
        }

        var bestResult = await SearchBestMatch(track);
        if (bestResult is null)
        {
            throw new InvalidOperationException(
                $"No matching YouTube result found for {track.Name} by {track.Artist}.");
        }

        logger.LogInformation("Downloading \"{name}\" from {url}", track.DisplayName, bestResult.Url);

        var tempDirectory = Path.Combine(GlobalConfiguration.MUSIC_DIRECTORY, ".tmp");
        Directory.CreateDirectory(tempDirectory);

        var tempFileTemplate = Path.Combine(tempDirectory, $"{track.SpotifyId}.%(ext)s");
        foreach (var file in Directory.GetFiles(tempDirectory, $"{track.SpotifyId}.*"))
        {
            File.Delete(file);
        }

        var downloadArgs = BuildDownloadArguments(bestResult.Url, tempFileTemplate);
        await RunYtDlp(downloadArgs, DownloadTimeout);

        var tempMatches = Directory.GetFiles(tempDirectory, $"{track.SpotifyId}.*");
        if (tempMatches.Length == 0)
        {
            throw new FileNotFoundException("yt-dlp did not produce the expected output file.");
        }

        var downloadedFilePath = tempMatches
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .First();

        foreach (var file in tempMatches)
        {
            if (!string.Equals(file, downloadedFilePath, StringComparison.Ordinal))
            {
                File.Delete(file);
            }
        }

        var extension = Path.GetExtension(downloadedFilePath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = $".{configuration.FORMAT}";
        }

        var outputFilePath = Path.Combine(outputDirectory, $"{outputFileName}{extension}");
        File.Move(downloadedFilePath, outputFilePath, overwrite: false);

        await WriteTags(outputFilePath, track);

        logger.LogInformation("Downloaded \"{name}\".", track.DisplayName);
    }

    /// <summary>
    /// Finds the best YouTube result for the Spotify track using the matching rules.
    /// </summary>
    private async Task<SpotdlResult?> SearchBestMatch(SpotdlTrack track)
    {
        var results = new Dictionary<SpotdlResult, double>();
        var isrcResults = new List<SpotdlResult>();

        if (!string.IsNullOrWhiteSpace(track.Isrc))
        {
            isrcResults = await Search(track.Isrc!, isrcSearch: true, limit: 5);
        }

        var searchQuery = CreateSongTitle(track.Name, track.Artists);
        var searchResults = await Search(searchQuery, isrcSearch: false, limit: 50);

        var combinedResults = isrcResults
            .Concat(searchResults)
            .DistinctBy(x => x.Url)
            .ToList();

        var ordered = OrderResults(combinedResults, track);
        foreach (var entry in ordered)
        {
            results[entry.Key] = entry.Value;
        }

        if (results.Count == 0)
        {
            return null;
        }

        var best = GetBestResult(results);
        return best.Result;
    }

    /// <summary>
    /// Runs yt-dlp search and returns parsed results.
    /// </summary>
    private async Task<List<SpotdlResult>> Search(string searchTerm, bool isrcSearch, int limit)
    {
        var args = new List<string>
        {
            "--dump-json",
            "--skip-download",
            "--no-playlist",
            "--no-warnings",
            "--quiet",
            "--ignore-errors",
            "--format",
            "bestaudio/best",
            "--extractor-args",
            "youtube:player_client=web",
            "--retries",
            "0",
            "--fragment-retries",
            "0",
        };

        args.AddRange(GetCookieOptions());
        args.Add($"ytsearch{limit}:{searchTerm}");

        var output = await RunYtDlp(args, SearchTimeout, allowNonZeroExitWhenOutput: true);
        var results = new List<SpotdlResult>();

        foreach (var line in output.StandardOutput.Where(line => !string.IsNullOrWhiteSpace(line)))
        {
            try
            {
                var data = JObject.Parse(line);
                var id = data.Value<string>("id") ?? "";
                var title = data.Value<string>("title") ?? "";
                var url = data.Value<string>("webpage_url") ?? $"https://www.youtube.com/watch?v={id}";
                var duration = data.Value<double?>("duration") ?? 0;
                var uploader = data.Value<string>("uploader") ?? data.Value<string>("channel") ?? "";
                var views = data.Value<long?>("view_count");

                var artists = new List<string>();
                if (data["artists"] is JArray artistsArray)
                {
                    artists.AddRange(
                        artistsArray.Values<string>()
                            .OfType<string>()
                            .Where(x => !string.IsNullOrWhiteSpace(x)));
                }
                else
                {
                    var artist = data.Value<string>("artist");
                    if (!string.IsNullOrWhiteSpace(artist))
                    {
                        artists.Add(artist);
                    }
                }

                if (artists.Count == 0 && !string.IsNullOrWhiteSpace(uploader))
                {
                    artists.Add(uploader);
                }

                var album = data.Value<string>("album");
                var hasMusicMeta = data["track"] != null || data["artist"] != null || data["album"] != null;

                results.Add(new SpotdlResult
                {
                    Source = "youtube",
                    Url = url,
                    Verified = hasMusicMeta,
                    Name = title,
                    Duration = duration,
                    Author = uploader,
                    ResultId = id,
                    IsrcSearch = isrcSearch,
                    SearchQuery = searchTerm,
                    Artists = artists.Count > 0 ? artists : null,
                    Views = views,
                    Explicit = null,
                    Album = album
                });
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed to parse yt-dlp JSON: {line}", line);
            }
        }

        return results;
    }

    /// <summary>
    /// Writes ID3 metadata and cover art to the downloaded file.
    /// </summary>
    private async Task WriteTags(string outputFilePath, SpotdlTrack track)
    {
        using var file = TagLibFile.Create(outputFilePath);
        file.Tag.Title = track.Name;
        file.Tag.Performers = track.Artists.ToArray();
        file.Tag.AlbumArtists = [track.AlbumArtist];
        file.Tag.Album = track.AlbumName;
        file.Tag.Track = (uint)Math.Max(track.TrackNumber, 0);
        file.Tag.Disc = (uint)Math.Max(track.DiscNumber, 0);
        if (track.Year.HasValue)
        {
            file.Tag.Year = track.Year.Value;
        }

        if (!string.IsNullOrWhiteSpace(track.CoverUrl))
        {
            try
            {
                var imageBytes = await HttpClient.GetByteArrayAsync(track.CoverUrl);
                var picture = new TagLibPicture(new TagLibByteVector(imageBytes))
                {
                    Type = TagLibPictureType.FrontCover,
                };
                file.Tag.Pictures = [picture];
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed to download cover art for {name}", track.DisplayName);
            }
        }

        file.Save();
    }

    /// <summary>
    /// Parses a simple command line string into argument tokens.
    /// </summary>
    private static List<string> ParseArgs(string? args)
    {
        if (string.IsNullOrWhiteSpace(args))
        {
            return [];
        }

        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        foreach (var ch in args)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(ch) && !inQuotes)
            {
                if (current.Length > 0)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }

                continue;
            }

            current.Append(ch);
        }

        if (current.Length > 0)
        {
            result.Add(current.ToString());
        }

        return result;
    }

    /// <summary>
    /// Builds yt-dlp arguments for audio download.
    /// </summary>
    private List<string> BuildDownloadArguments(string url, string outputFileTemplate)
    {
        var args = new List<string>
        {
            "--no-playlist",
            "--no-warnings",
            "--quiet",
            "--extractor-args",
            "youtube:player_client=web",
            "--retries",
            "0",
            "--fragment-retries",
            "0",
            "-x",
            "--audio-format",
            configuration.FORMAT,
            "-o",
            outputFileTemplate,
            url
        };

        var extraArgs = ParseArgs(configuration.OPTIONS);
        if (extraArgs.Count == 0)
        {
            return args;
        }

        var skipOptionNames = new[]
        {
            "-o",
            "-P",
            "--bitrate",
            "--threads",
            "--client-id",
            "--client-secret",
            "--format",
            "--audio-format",
            "--output",
            "--paths"
        };
        var skipOptions = new HashSet<string>(skipOptionNames, StringComparer.OrdinalIgnoreCase);
        var longOptions = skipOptionNames.Where(option => option.StartsWith("--", StringComparison.Ordinal)).ToArray();
        var shortOptions = skipOptionNames
            .Where(option => option.StartsWith("-", StringComparison.Ordinal) &&
                             !option.StartsWith("--", StringComparison.Ordinal))
            .ToArray();

        for (var index = 0; index < extraArgs.Count; index++)
        {
            var arg = extraArgs[index];
            if (ShouldSkipOption(arg, skipOptions, longOptions, shortOptions, out var skipNext))
            {
                if (skipNext)
                {
                    index += 1;
                }

                continue;
            }

            args.Add(arg);
        }

        return args;

        static bool ShouldSkipOption(
            string arg,
            ISet<string> skipOptions,
            IReadOnlyList<string> longOptions,
            IReadOnlyList<string> shortOptions,
            out bool skipNext)
        {
            skipNext = false;

            if (skipOptions.Contains(arg))
            {
                skipNext = true;
                return true;
            }

            foreach (var shortOption in shortOptions)
            {
                if (arg.StartsWith(shortOption, StringComparison.Ordinal) && arg.Length > shortOption.Length)
                {
                    return true;
                }
            }

            foreach (var option in longOptions)
            {
                var prefix = $"{option}=";
                if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Executes yt-dlp and captures stdout/stderr.
    /// </summary>
    private async Task<ProcessOutput> RunYtDlp(
        IReadOnlyList<string> args,
        TimeSpan timeout,
        bool allowNonZeroExitWhenOutput = false)
    {
        var ytDlpPath = File.Exists("/env/bin/yt-dlp") ? "/env/bin/yt-dlp" : "yt-dlp";
        var startInfo = new ProcessStartInfo
        {
            FileName = ytDlpPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            throw new InvalidOperationException("Failed to start yt-dlp process.");
        }

        var outputLines = new List<string>();
        var errorLines = new List<string>();
        var botVerificationChallengeTriggered = false;

        var outputTask = Task.Run(async () =>
        {
            while (true)
            {
                if (botVerificationChallengeTriggered)
                {
                    break;
                }

                var line = await process.StandardOutput.ReadLineAsync();
                if (line is null)
                {
                    break;
                }

                outputLines.Add(line);
            }
        });

        var errorTask = Task.Run(async () =>
        {
            while (true)
            {
                if (botVerificationChallengeTriggered)
                {
                    break;
                }

                var line = await process.StandardError.ReadLineAsync();
                if (line is null)
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                errorLines.Add(line);
                if (IsBotChallenge(line))
                {
                    botVerificationChallengeTriggered = true;
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill(entireProcessTree: true);
                        }
                    }
                    catch
                    {
                        // Ignore kill failures
                    }

                    break;
                }
            }
        });

        using var cts = new CancellationTokenSource(timeout);
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Ignore kill failures
            }

            throw new TimeoutException("yt-dlp took too long and was terminated.");
        }
        finally
        {
            await Task.WhenAll(outputTask, errorTask);
        }

        if (botVerificationChallengeTriggered)
        {
            var combinedError = string.Join(Environment.NewLine, errorLines);
            throw new InvalidOperationException(
                "yt-dlp was stopped due to a bot verification challenge. " +
                "Provide cookies or reduce request intensity. " +
                combinedError);
        }

        if (process.ExitCode != 0)
        {
            var combinedError = string.Join(Environment.NewLine, errorLines);
            if (allowNonZeroExitWhenOutput && outputLines.Count > 0)
            {
                logger.LogWarning(
                    "yt-dlp exited with {code} during search. Continuing with {count} results. Errors: {errors}",
                    process.ExitCode,
                    outputLines.Count,
                    combinedError);
            }
            else
            {
                throw new InvalidOperationException($"yt-dlp failed with exit code {process.ExitCode}: {combinedError}");
            }
        }

        return new ProcessOutput
        {
            StandardOutput = outputLines,
            StandardError = errorLines
        };
    }

    private static bool IsBotChallenge(string line)
    {
        return line.Contains("Sign in to confirm you're not a bot", StringComparison.OrdinalIgnoreCase)
               || line.Contains("Use --cookies", StringComparison.OrdinalIgnoreCase)
               || line.Contains("cookies-from-browser", StringComparison.OrdinalIgnoreCase)
               || line.Contains("Cookies file must be Netscape formatted, not JSON.", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Extracts a Spotify item id from a URL.
    /// </summary>
    private static bool TryParseSpotifyId(string url, string type, out string id)
    {
        id = string.Empty;

        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        var match = System.Text.RegularExpressions.Regex.Match(
            url,
            @$"/.*\.spotify.com\/.*{type}\/([^\?]+)(\?.+)?",
            System.Text.RegularExpressions.RegexOptions.Compiled);

        if (!match.Success)
        {
            return false;
        }

        id = match.Groups[1].Value;
        return !string.IsNullOrWhiteSpace(id);
    }

    /// <summary>
    /// Builds the search string using artists and title.
    /// </summary>
    private static string CreateSongTitle(string songName, IReadOnlyList<string> artists)
        => artists.Count > 0 ? $"{string.Join(", ", artists)} - {songName}" : songName;

    /// <summary>
    /// Scores results using the spotdl matching logic.
    /// </summary>
    private static Dictionary<SpotdlResult, double> OrderResults(IEnumerable<SpotdlResult> results, SpotdlTrack song)
    {
        var linksWithMatchValue = new Dictionary<SpotdlResult, double>();

        foreach (var result in results)
        {
            if (!CheckCommonWord(song, result))
            {
                continue;
            }

            var artistsMatch = CalcMainArtistMatch(song, result);
            var otherArtistsMatch = CalcArtistsMatch(song, result);
            artistsMatch += otherArtistsMatch;
            artistsMatch = artistsMatch / (song.Artists.Count > 1 ? 2 : 1);

            artistsMatch = ArtistsMatchFixup1(song, result, artistsMatch);
            artistsMatch = ArtistsMatchFixup2(song, result, artistsMatch);
            artistsMatch = ArtistsMatchFixup3(song, result, artistsMatch);

            var nameMatch = CalcNameMatch(song, result);

            var (containsForbidden, forbiddenWords) = CheckForbiddenWords(song, result);
            if (containsForbidden)
            {
                nameMatch -= forbiddenWords.Count * 15;
            }

            var albumMatch = CalcAlbumMatch(song, result);
            var timeMatch = CalcTimeMatch(song, result);

            if (nameMatch <= 60)
            {
                continue;
            }

            if (artistsMatch < 70)
            {
                continue;
            }

            var averageMatch = (artistsMatch + nameMatch) / 2;

            if (result.Verified && !result.IsrcSearch && !string.IsNullOrWhiteSpace(result.Album) && albumMatch <= 80)
            {
                averageMatch = (averageMatch + albumMatch) / 2;
            }

            if (timeMatch < 25)
            {
                continue;
            }

            if (timeMatch < 50 && averageMatch < 75)
            {
                continue;
            }

            if (!result.IsrcSearch && averageMatch <= 85)
            {
                averageMatch = (averageMatch + timeMatch) / 2;

                if (result.Explicit.HasValue && song.Explicit.HasValue && result.Explicit != song.Explicit)
                {
                    averageMatch -= 5;
                }
            }

            averageMatch = Math.Min(averageMatch, 100);
            linksWithMatchValue[result] = averageMatch;
        }

        return linksWithMatchValue;
    }

    /// <summary>
    /// Chooses the best match using score and view count.
    /// </summary>
    private static BestMatch GetBestResult(Dictionary<SpotdlResult, double> results)
    {
        var bestResults = GetBestMatches(results, 8);
        if (bestResults.Count == 1)
        {
            return new BestMatch { Result = bestResults[0].Result, Score = bestResults[0].Score };
        }

        var best = bestResults[0];
        if (best is { Score: > 80, Result.IsrcSearch: true })
        {
            return new BestMatch { Result = best.Result, Score = best.Score };
        }

        if (bestResults.Count <= 1)
        {
            return new BestMatch { Result = best.Result, Score = best.Score };
        }

        var views = bestResults.Select(x => x.Result.Views ?? 0).ToList();
        var highestViews = views.Max();
        var lowestViews = views.Min();

        if (highestViews == 0 || highestViews == lowestViews)
        {
            return new BestMatch { Result = best.Result, Score = best.Score };
        }

        var weighted = new List<BestMatch>();
        for (var index = 0; index < bestResults.Count; index++)
        {
            var resultViews = views[index];
            var viewsScore = (double)(resultViews - lowestViews) / (highestViews - lowestViews) * 15;
            var score = Math.Min(bestResults[index].Score + viewsScore, 100);
            weighted.Add(new BestMatch { Result = bestResults[index].Result, Score = score });
        }

        var max = weighted.MaxBy(x => x.Score);
        return max ?? new BestMatch { Result = best.Result, Score = best.Score };
    }

    /// <summary>
    /// Keeps results within a score window of the best match.
    /// </summary>
    private static List<BestMatch> GetBestMatches(Dictionary<SpotdlResult, double> results, double scoreThreshold)
    {
        var sortedResults = results
            .Select(x => new BestMatch { Result = x.Key, Score = x.Value })
            .OrderByDescending(x => x.Score)
            .ToList();

        var bestScore = sortedResults[0].Score;
        return sortedResults.Where(x => (bestScore - x.Score) <= scoreThreshold).ToList();
    }

    /// <summary>
    /// Rejects results without any word overlap in the title.
    /// </summary>
    private static bool CheckCommonWord(SpotdlTrack song, SpotdlResult result)
    {
        var sentenceWords = Slugify(song.Name).Split("-", StringSplitOptions.RemoveEmptyEntries);
        var toCheck = Slugify(result.Name).Replace("-", "");

        return sentenceWords.Any(word => word.Length > 0 && toCheck.Contains(word));
    }

    /// <summary>
    /// Detects forbidden words in the result title.
    /// </summary>
    private static (bool contains, List<string> words) CheckForbiddenWords(SpotdlTrack song, SpotdlResult result)
    {
        var forbidden = new[]
        {
            "bassboosted",
            "remix",
            "remastered",
            "remaster",
            "reverb",
            "bassboost",
            "live",
            "acoustic",
            "8daudio",
            "concert",
            "acapella",
            "slowed",
            "instrumental",
            "cover",
        };

        var songName = Slugify(song.Name).Replace("-", "");
        var toCheck = Slugify(result.Name).Replace("-", "");

        var words = forbidden.Where(word => toCheck.Contains(word) && !songName.Contains(word)).ToList();

        return (words.Count > 0, words);
    }

    /// <summary>
    /// Creates normalized strings for name matching.
    /// </summary>
    private static (string, string) CreateMatchStrings(SpotdlTrack song, SpotdlResult result)
    {
        var slugSongName = Slugify(song.Name);
        var slugSongTitle = Slugify(CreateSongTitle(song.Name, song.Artists));
        var testStr1 = Slugify(result.Name);
        var testStr2 = result.Verified ? slugSongName : slugSongTitle;

        testStr1 = FillString(song.Artists, testStr1, testStr2);
        testStr2 = FillString(song.Artists, testStr2, testStr1);

        var (testList1, testList2) = BasedSort(testStr1.Split("-").ToList(), testStr2.Split("-").ToList());
        testStr1 = string.Join("-", testList1);
        testStr2 = string.Join("-", testList2);

        return (testStr1, testStr2);
    }

    /// <summary>
    /// Adds missing artist tokens when they exist in the other string.
    /// </summary>
    private static string FillString(IReadOnlyList<string> strings, string mainString, string stringToCheck)
    {
        var finalStr = mainString;
        var testStr = finalStr.Replace("-", "");
        var simpleTestStr = stringToCheck.Replace("-", "");

        foreach (var str in strings)
        {
            var slugStr = Slugify(str).Replace("-", "");
            if (simpleTestStr.Contains(slugStr) && !testStr.Contains(slugStr))
            {
                finalStr += $"-{slugStr}";
                testStr += slugStr;
            }
        }

        return finalStr;
    }

    /// <summary>
    /// Builds a normalized artist token string for fuzzy matching.
    /// </summary>
    private static string CreateCleanString(IEnumerable<string> words, string text, bool sort, string joinStr = "-")
    {
        text = Slugify(text).Replace("-", "");
        var final = words
            .Select(word => Slugify(word).Replace("-", ""))
            .Where(slug => !text.Contains(slug))
            .ToList();

        return sort
            ? string.Join(joinStr, final.OrderBy(x => x))
            : string.Join(joinStr, final);
    }

    /// <summary>
    /// Sorts tokens based on a reference list.
    /// </summary>
    private static (List<string>, List<string>) BasedSort(List<string> strings, List<string> basedOn)
    {
        strings.Sort();
        basedOn.Sort();

        var map = basedOn
            .Select((value, index) => new { value, index })
            .ToDictionary(x => x.value, x => x.index);

        var sorted = strings.OrderByDescending(x => map.TryGetValue(x, out var index) ? index : -1).ToList();
        basedOn.Reverse();
        return (sorted, basedOn);
    }

    /// <summary>
    /// Scores the main artist match.
    /// </summary>
    private static double CalcMainArtistMatch(SpotdlTrack song, SpotdlResult result)
    {
        if (result.Artists is null || result.Artists.Count == 0)
        {
            return 0;
        }

        var songArtists = song.Artists.Select(Slugify).ToList();
        var resultArtists = result.Artists.Select(Slugify).ToList();
        var (sortedSongArtists, sortedResultArtists) = BasedSort(songArtists, resultArtists);

        var slugSongMainArtist = Slugify(song.Artists[0]);
        var slugResultMainArtist = sortedResultArtists[0];

        if (song.Artists.Count > 1 && result.Artists.Count == 1)
        {
            var match = 0.0;
            foreach (var artist in song.Artists.Skip(1))
            {
                var slugArtist = string.Join("-", Slugify(artist).Split("-").OrderBy(x => x));
                var resultMain = string.Join("-", slugResultMainArtist.Split("-").OrderBy(x => x));
                if (resultMain.Contains(slugArtist))
                {
                    match += 100.0 / song.Artists.Count;
                }
            }

            return match;
        }

        var mainArtistMatch = Ratio(slugSongMainArtist, slugResultMainArtist);

        if (mainArtistMatch < 50 && song.Artists.Count > 1)
        {
            foreach (var songArtist in sortedSongArtists.Take(2))
            {
                mainArtistMatch = sortedResultArtists
                    .Take(2)
                    .Select(resultArtist => Ratio(songArtist, resultArtist))
                    .Prepend(mainArtistMatch)
                    .Max();
            }
        }

        return mainArtistMatch;
    }

    /// <summary>
    /// Scores the secondary artists match.
    /// </summary>
    private static double CalcArtistsMatch(SpotdlTrack song, SpotdlResult result)
    {
        if (song.Artists.Count == 1 || result.Artists is null || result.Artists.Count == 0)
        {
            return 0;
        }

        var artist1List = song.Artists.Select(Slugify).ToList();
        var artist2List = result.Artists.Select(Slugify).ToList();
        var (sortedArtist1, sortedArtist2) = BasedSort(artist1List, artist2List);

        sortedArtist1 = sortedArtist1.Skip(1).ToList();
        sortedArtist2 = sortedArtist2.Skip(1).ToList();

        var matches = 0.0;
        var max = Math.Max(sortedArtist1.Count, sortedArtist2.Count);
        for (var index = 0; index < max; index++)
        {
            var a1 = index < sortedArtist1.Count ? sortedArtist1[index] : "";
            var a2 = index < sortedArtist2.Count ? sortedArtist2[index] : "";
            matches += Ratio(a1, a2);
        }

        return sortedArtist1.Count == 0 ? 0 : matches / sortedArtist1.Count;
    }

    /// <summary>
    /// Fixups for low artist match on unverified results.
    /// </summary>
    private static double ArtistsMatchFixup1(SpotdlTrack song, SpotdlResult result, double score)
    {
        if (result.Verified || score > 50)
        {
            return score;
        }

        var channelMatch = Ratio(Slugify(song.Artist), Slugify(result.Author));
        score = Math.Max(score, channelMatch);

        if (score <= 70)
        {
            var resultName = Slugify(result.Name).Replace("-", "");
            var artistTitleMatch = song.Artists
                .Select(artist => Slugify(artist).Replace("-", ""))
                .Where(slugArtist => resultName.Contains(slugArtist))
                .Sum(x => 1.0);

            artistTitleMatch = (artistTitleMatch / song.Artists.Count) * 100;
            score = Math.Max(score, artistTitleMatch);
        }

        if (score <= 70)
        {
            var artistList1 = song.Artists.SelectMany(x => Slugify(x).Split("-")).ToList();
            var artistList2 = result.Artists?.SelectMany(x => Slugify(x).Split("-")).ToList() ?? [];
            var artistTitleMatch = Ratio(string.Join("", artistList1), string.Join("", artistList2));
            score = Math.Max(score, artistTitleMatch);
        }

        return score;
    }

    /// <summary>
    /// Fixups for verified results when artist score is low.
    /// </summary>
    private static double ArtistsMatchFixup2(SpotdlTrack song, SpotdlResult result, double score)
    {
        if (score > 70 || !result.Verified)
        {
            return score;
        }

        var slugSongName = Slugify(song.Name);
        var slugResultName = Slugify(result.Name);

        var hasMainArtist = (score / (song.Artists.Count > 1 ? 2 : 1)) > 50;
        var (_, matchStr2) = CreateMatchStrings(song, result);

        var artistsToCheck = song.Artists.Skip(hasMainArtist ? 1 : 0);
        score = artistsToCheck
            .Select(artist => Slugify(artist).Replace("-", ""))
            .Where(slugArtist => matchStr2.Replace("-", "").Contains(slugArtist))
            .Aggregate(score, (current, slugArtist) => current + 5);

        if (score <= 70)
        {
            var artistList1 = CreateCleanString(song.Artists, slugSongName, true);
            var artistList2 = CreateCleanString(
                result.Artists ?? new List<string> { result.Author },
                slugResultName,
                true);
            var artistTitleMatch = Ratio(artistList1, artistList2);
            score = Math.Max(score, artistTitleMatch);
        }

        return score;
    }

    /// <summary>
    /// Fixup for single-artist results when the song has multiple artists.
    /// </summary>
    private static double ArtistsMatchFixup3(SpotdlTrack song, SpotdlResult result, double score)
    {
        if (score > 70
            || result.Artists is null
            || result.Artists.Count > 1
            || song.Artists.Count == 1)
        {
            return score;
        }

        var fixup = Ratio(
            Slugify(result.Name),
            Slugify(CreateSongTitle(song.Name, [song.Artist])));

        if (fixup >= 80)
        {
            score = (score + fixup) / 2;
        }

        return Math.Min(score, 100);
    }

    /// <summary>
    /// Scores the title match between Spotify and YouTube.
    /// </summary>
    private static double CalcNameMatch(SpotdlTrack song, SpotdlResult result)
    {
        var (matchStr1, matchStr2) = CreateMatchStrings(song, result);
        var resultName = Slugify(result.Name);
        var songName = Slugify(song.Name);

        var (resList, songList) = BasedSort(resultName.Split("-").ToList(), songName.Split("-").ToList());
        resultName = string.Join("-", resList);
        songName = string.Join("-", songList);

        var nameMatch = Ratio(resultName, songName);
        if (nameMatch <= 75)
        {
            var secondMatch = Ratio(matchStr1, matchStr2);
            nameMatch = Math.Max(nameMatch, secondMatch);
        }

        return nameMatch;
    }

    /// <summary>
    /// Scores the duration match using exponential decay.
    /// </summary>
    private static double CalcTimeMatch(SpotdlTrack song, SpotdlResult result)
    {
        var diff = Math.Abs(song.DurationSeconds - result.Duration);
        return Math.Exp(-0.1 * diff) * 100;
    }

    /// <summary>
    /// Scores the album match when metadata is available.
    /// </summary>
    private static double CalcAlbumMatch(SpotdlTrack song, SpotdlResult result)
    {
        if (string.IsNullOrWhiteSpace(result.Album))
        {
            return 0;
        }

        return Ratio(Slugify(song.AlbumName), Slugify(result.Album));
    }

    /// <summary>
    /// Normalizes strings to a slug for fuzzy matching.
    /// </summary>
    private static string Slugify(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        var lastDash = false;

        foreach (var ch in normalized.Where(ch =>
                     CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark))
        {
            if (char.IsLetterOrDigit(ch) || ch == '-' || ch == '!' || ch == '@' || ch == '$')
            {
                sb.Append(ch);
                lastDash = false;
                continue;
            }

            if (!lastDash)
            {
                sb.Append('-');
                lastDash = true;
            }
        }

        return sb.ToString().Trim('-');
    }

    /// <summary>
    /// Returns a similarity ratio based on Levenshtein distance.
    /// </summary>
    private static double Ratio(string? string1, string? string2)
    {
        if (string.IsNullOrEmpty(string1) && string.IsNullOrEmpty(string2))
        {
            return 100;
        }

        if (string.IsNullOrEmpty(string1) || string.IsNullOrEmpty(string2))
        {
            return 0;
        }

        var distance = LevenshteinDistance(string1, string2);
        var max = Math.Max(string1.Length, string2.Length);
        if (max == 0)
        {
            return 100;
        }

        return (1.0 - (double)distance / max) * 100;
    }

    /// <summary>
    /// Computes Levenshtein distance between two strings.
    /// </summary>
    private static int LevenshteinDistance(string source, string target)
    {
        var n = source.Length;
        var m = target.Length;
        var d = new int[n + 1, m + 1];

        for (var i = 0; i <= n; i++)
        {
            d[i, 0] = i;
        }

        for (var j = 0; j <= m; j++)
        {
            d[0, j] = j;
        }

        for (var i = 1; i <= n; i++)
        {
            for (var j = 1; j <= m; j++)
            {
                var cost = source[i - 1] == target[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost
                );
            }
        }

        return d[n, m];
    }

    private List<string> GetCookieOptions()
    {
        var extraArgs = ParseArgs(configuration.OPTIONS);
        if (extraArgs.Count == 0)
        {
            return [];
        }

        var cookieOptions = new List<string>();
        var cookieNames = new[]
        {
            "--cookies",
            "--cookies-from-browser"
        };

        for (var index = 0; index < extraArgs.Count; index++)
        {
            var arg = extraArgs[index];

            if (cookieNames.Any(option => string.Equals(arg, option, StringComparison.OrdinalIgnoreCase)))
            {
                if (index + 1 < extraArgs.Count)
                {
                    cookieOptions.Add(arg);
                    cookieOptions.Add(extraArgs[index + 1]);
                    index += 1;
                }

                continue;
            }

            foreach (var option in cookieNames)
            {
                var prefix = $"{option}=";
                if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    cookieOptions.Add(arg);
                    break;
                }
            }
        }

        return cookieOptions;
    }
}
