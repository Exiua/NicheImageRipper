using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace Core;

public partial class BunnyVideoDrm
{
    private static readonly HttpClient Session = new();
    private static readonly Random Random = new();
    private static readonly Dictionary<string, string> UserAgent = new()
    {
        ["sec-ch-ua"] = """
                        "Google Chrome";v="107", "Chromium";v="107", "Not=A?Brand";v="24"
                        """,
        ["sec-ch-ua-mobile"] = "?0",
        ["sec-ch-ua-platform"] = """
                                 "Linux"
                                 """,
        ["user-agent"] =
            "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/107.0.0.0 Safari/537.36"
    };

    static BunnyVideoDrm()
    {
        foreach (var (key, value) in UserAgent)
        {
            Session.DefaultRequestHeaders.Add(key, value);
        }
    }

    public string Referer { get; set; }
    public string EmbedUrl { get; set; }
    public string Guid { get; set; }

    public Dictionary<string, Dictionary<string, string>> Headers { get; set; } = new()
    {
        ["embed"] = new Dictionary<string, string>
        {
            ["authority"] = "iframe.mediadelivery.net",
            ["accept"] = "*/*",
            ["accept-language"] = "en-US,en;q=0.9",
            ["cache-control"] = "no-cache",
            ["pragma"] = "no-cache",
            //["referer"] = Referer
            ["sec-fetch-dest"] = "iframe",
            ["sec-fetch-mode"] = "navigate",
            ["sec-fetch-site"] = "cross-site",
            ["upgrade-insecure-requests"] = "1"
            },
        ["ping|activate"] = new Dictionary<string, string>
        {
            ["accept"] = "*/*",
            ["accept-language"] = "en-US,en;q=0.9",
            ["cache-control"] = "no-cache",
            ["origin"] = "https://iframe.mediadelivery.net",
            ["pragma"] = "no-cache",
            ["referer"] = "https://iframe.mediadelivery.net/",
            ["sec-fetch-dest"] = "empty",
            ["sec-fetch-mode"] = "cors",
            ["sec-fetch-site"] = "same-site"
        },
        ["playlist"] = new Dictionary<string, string>
        {
            ["authority"] = "iframe.mediadelivery.net",
            ["accept"] = "*/*",
            ["accept-language"] = "en-US,en;q=0.9",
            ["cache-control"] = "no-cache",
            ["pragma"] = "no-cache",
            //["referer"] = embed_url,
            ["sec-fetch-dest"] = "empty",
            ["sec-fetch-mode"] = "cors",
            ["sec-fetch-site"] = "same-origin"
        }
    };
    public string ServerId { get; set; }
    public string ContextId { get; set; }
    public string Secret { get; set; }
    public string Filename { get; set; }
    public string Path { get; set; }
    
    public BunnyVideoDrm(string referer = "https://127.0.0.1/", string embedUrl = "", string name = "", string path = "")
    {
        Referer = !string.IsNullOrEmpty(referer) ? referer : throw new Exception();
        EmbedUrl = !string.IsNullOrEmpty(embedUrl) ? embedUrl : throw new Exception();
        Guid = new Uri(embedUrl).AbsolutePath.Split('/')[^1];
        Headers["embed"]["referer"] = Referer;
        Headers["playlist"]["referer"] = EmbedUrl;
        var requestMessage = Headers["embed"].ToRequest(HttpMethod.Get, EmbedUrl);
        var embedResponse = Session.Send(requestMessage);
        var embedPage = embedResponse.Content.ReadAsStringAsync().Result;
        var match = ServerIdRegex().Match(embedPage);
        if (match.Success)
        {
            ServerId = match.Groups[1].Value;
        }
        else
        {
            throw new Exception();
        }
        
        Headers["ping|activate"].Add("authority", $"video-{ServerId}.mediadelivery.net");
        var search = ContextIdAndSecretRegex().Match(embedPage);
        ContextId = search.Groups[1].Value;
        Secret = search.Groups[2].Value;
        if (!string.IsNullOrEmpty(name))
        {
            Filename = $"{name}.mp4";
        }
        else
        {
            var fileUnescaped = FileUnescapedRegex().Match(embedPage).Groups[1].Value;
            var fileEscaped = HttpUtility.HtmlDecode(fileUnescaped);
            Filename = FilenameRegex().Replace(fileEscaped, ".mp4");
        }
        Path = !string.IsNullOrEmpty(path) ? path : "~/Videos/Bunny CDN/";
    }

    private string PrepareDl()
    {
        Ping(time: 0, paused: "true", resolution: "0");
        Activate();
        var resolution = MainPlaylist();
        VideoPlaylist();
        for (var i = 0; i < 29; i += 4) // first 28 seconds, arbitrary
        {
            Ping(time: i + (int) Math.Round(Random.NextDouble(), 6), paused: "false", resolution: resolution.Split('x')[^1]);
        }
        Session.Dispose();
        return resolution;

        void Ping(int time, string paused, string resolution)
        {
            var md5Hash = MD5.HashData(Encoding.UTF8.GetBytes($"{Secret}_{ContextId}_{time}_{paused}_{resolution}"))
                .HexDigest();
            var parameters = new Dictionary<string, string>
            {
                ["hash"] = md5Hash,
                ["time"] = time.ToString(),
                ["paused"] = paused,
                ["chosen_res"] = resolution
            };
            var requestUrl =
                $"https://video-{ServerId}.mediadelivery.net/.drm/{ContextId}/ping".ToQueryString(parameters);
            var requestMessage = Headers["ping|activate"].ToRequest(HttpMethod.Get, requestUrl);
            Session.Send(requestMessage);
        }

        void Activate()
        {
            var requestUrl = $"https://video-{ServerId}.mediadelivery.net/.drm/{ContextId}/activate";
            var requestMessage = Headers["ping|activate"].ToRequest(HttpMethod.Get, requestUrl);
            Session.Send(requestMessage);
        }

        string MainPlaylist()
        {
            var parameters = new Dictionary<string, string>
            {
                ["contextId"] = ContextId,
                ["secret"] = Secret
            };
            var requestUrl = $"https://iframe.mediadelivery.net/{Guid}/playlist.drm".ToQueryString(parameters);
            var requestMessage = Headers["playlist"].ToRequest(HttpMethod.Get, requestUrl);
            var response = Session.Send(requestMessage);
            var resolutions = ResolutionRegex().Matches(response.Content.ReadAsStringAsync().Result);
            if (resolutions.Count == 0)
            {
                throw new FileNotFoundException();
            }

            // highest resolution, 0 for lowest
            return resolutions[^1].Groups[1].Value;
        }

        void VideoPlaylist()
        {
            var parameters = new Dictionary<string, string>
            {
                ["contextId"] = ContextId
            };
            var requestUrl = $"https://iframe.mediadelivery.net/{Guid}/{resolution}/video.drm"
                .ToQueryString(parameters);
            var requestMessage = Headers["playlist"].ToRequest(HttpMethod.Get, requestUrl);
            Session.Send(requestMessage);
        }
    }

    public async Task Download()
    {
        var resolution = PrepareDl();
        string[] url =
            [ $"https://iframe.mediadelivery.net/{Guid}/{resolution}/video.drm?contextId={ContextId}" ];
        /*var ydlOpts = new Dictionary<string, object>
        {
            ["http_headers"] = new Dictionary<string, string>
            {
                ["Referer"] = EmbedUrl,
                ["User-Agent"] = UserAgent["user-agent"]
            },
            ["concurrent_fragment_downloads"] = 10,
            ["nocheckcertificate"] = true,
            ["outtmpl"] = Filename,
            ["restrictfilenames"] = true,
            ["windowsfilenames"] = true,
            ["nopart"] = true,
            ["paths"] = new Dictionary<string, string>
            {
                ["home"] = Path,
                ["temp"] = $".{Filename}/"
            },
            ["retries"] = float.PositiveInfinity,
            ["extractor_retries"] = float.PositiveInfinity,
            ["fragment_retries"] = float.PositiveInfinity,
            ["skip_unavailable_fragments"] = false,
            ["no_warnings"] = true
        };*/
        await DownloadVideoAsync(url[0]);
    }

    private async Task DownloadVideoAsync(string url)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "yt-dlp", // Ensure yt-dlp is accessible in the system's PATH or specify the full path to the executable
            Arguments = BuildArguments(url),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };

        using var process = new Process();
        process.StartInfo = startInfo;
        process.Start();

        // Asynchronously read the standard output of the spawned process. 
        // This raises OutputDataReceived events for each line of output.
        var output = await process.StandardOutput.ReadToEndAsync();
        Console.WriteLine(output);

        await process.WaitForExitAsync();
    }

    private string BuildArguments(string url)
    {
        // Converts the Python dictionary of options into yt-dlp command-line arguments.
        return $"--add-headers Referer:\"{EmbedUrl}\" --add-headers User-Agent:\"{UserAgent["user-agent"]}\" " +
               $"--concurrent-fragments 10 --no-check-certificates --output \"{Filename}\" " +
               $"--restrict-filenames --windows-filenames --no-part --paths home:\"{Path}\",temp:\".{Filename}/\" " +
               $"--retries infinite --extractor-retries infinite --fragment-retries infinite " +
               $"--skip-unavailable-fragments --no-warnings \"{url}\"";
    }

    [GeneratedRegex(@"https://video-(.*?)\.mediadelivery\.net")]
    private static partial Regex ServerIdRegex();
    [GeneratedRegex(@"contextId=(.*?)&secret=(.*?)""")]
    private static partial Regex ContextIdAndSecretRegex();
    [GeneratedRegex(@"og:title"" content=""(.*?)""")]
    private static partial Regex FileUnescapedRegex();
    [GeneratedRegex(@"\.[^.]*$.*")]
    private static partial Regex FilenameRegex();
    [GeneratedRegex("RESOLUTION=(.*)")]
    private static partial Regex ResolutionRegex();
}