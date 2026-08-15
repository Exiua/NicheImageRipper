using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Exceptions;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.Youtube;

public class YoutubeParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "youtube";
    public static string[] SupportedUrls => ["https://www.youtube.com/"];
    protected override bool RequiresNavigation => false;

    public YoutubeParser(WebDriver driver, ApiClientManager apiClientManager, Dictionary<string, string> requestHeaders,
                         FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, apiClientManager,
        requestHeaders, IHtmlParser.GetFilenameScheme<YoutubeParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for site and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        if (!Core.NicheImageRipper.AvailableFeatures.HasFlag(ExternalFeatureSupport.YtDlp))
        {
            Logger.Error("yt-dlp is not available. Cannot parse YouTube without yt-dlp.");
            throw new FeatureNotAvailableException(ExternalFeatureSupport.YtDlp);
        }

        if (FilenameScheme != FilenameScheme.Original)
        {
            Logger.Warning(
                "YoutubeParser only supports Original filename scheme. Files will be saved with original filenames.");
        }

        var url = GivenUrl.Split("/").Take(4).Join('/');

        var metadataArgs = new SubprocessArgs("yt-dlp")
                          .WithArgs("--flat-playlist", "--dump-single-json", "--encoding", "utf-8", url)
                          .EnableOutputCapture()
                          .EnableErrorCapture();
        var (exitCode, output, error) = await RunSubprocess(metadataArgs, cancellationToken: cancellationToken);
        if (exitCode != 0 || string.IsNullOrWhiteSpace(output))
        {
            Logger.Error("yt-dlp failed with exit code {ExitCode}. Error: {Error}", exitCode, error);
            throw new RipperException("yt-dlp subprocess failed");
        }

        var json = JsonSerializer.Deserialize<JsonNode>(output)!.AsObject();
        var entries = json["entries"]?.AsArray();

        return entries is not null
            ? ParseChannel(json, entries)
            : ParseSingleVideo(json);
    }

    private RipInfo ParseChannel(JsonObject json, JsonArray entries)
    {
        var displayName = json["channel"]?.GetValue<string>() ?? json["uploader"]?.GetValue<string>();
        var username = json["uploader_id"]?.GetValue<string>() ?? json["channel_id"]?.GetValue<string>();
        var dirName = displayName is null
            ? "Unknown Channel"
            : username is null
                ? displayName
                : $"{displayName} ({username})";

        var images = entries.Select(entry =>
        {
            var title = entry!["title"]!.GetValue<string>();
            var id = entry["id"]!.GetValue<string>();
            var videoUrl = $"https://www.youtube.com/watch?v={id}";
            return FileLink.WithFilename(videoUrl, $"{title}.webm", FilenameScheme, cleanFilename: true);
        }).ToStringImageLinkWrapperList();

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }

    private RipInfo ParseSingleVideo(JsonObject json)
    {
        var title = json["title"]?.GetValue<string>() ?? "Unknown Video";
        var id = json["id"]!.GetValue<string>();
        var videoUrl = $"https://www.youtube.com/watch?v={id}";

        var fileLink = FileLink.WithFilename(videoUrl, $"{title}.webm", FilenameScheme, cleanFilename: true);
        List<StringFileLinkWrapper> images = [fileLink];

        return RipInfo.FromUrlList(images, title, FilenameScheme);
    }

    private class SubprocessArgs
    {
        public string Executable { get; set; }
        public List<string> Arguments { get; set; }
        public bool CaptureOutput { get; set; }
        public bool CaptureError { get; set; }

        public SubprocessArgs(string executable)
        {
            Executable = executable;
            Arguments = [];
        }

        public SubprocessArgs WithArg(string arg)
        {
            Arguments.Add(arg);
            return this;
        }

        public SubprocessArgs WithArgs(params string[] args)
        {
            Arguments.AddRange(args);
            return this;
        }

        public SubprocessArgs EnableOutputCapture()
        {
            CaptureOutput = true;
            return this;
        }

        public SubprocessArgs EnableErrorCapture()
        {
            CaptureError = true;
            return this;
        }

        public string GetArgs()
        {
            return Arguments.Count == 0 ? "" : string.Join(" ", Arguments);
        }
    }

    private static async Task<(int, string?, string? )> RunSubprocess(SubprocessArgs args,
                                                                      CancellationToken cancellationToken = default)
    {
        var arguments = args.GetArgs();
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = args.Executable,
            Arguments = arguments,
            RedirectStandardOutput = args.CaptureOutput,
            StandardOutputEncoding = args.CaptureOutput ? Encoding.UTF8 : null,
            RedirectStandardError = args.CaptureError,
            StandardErrorEncoding = args.CaptureError ? Encoding.UTF8 : null,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        string? output = null;
        if (args.CaptureOutput)
        {
            output = "";
            process.OutputDataReceived += (_, eventArgs) =>
            {
                if (eventArgs.Data is not null)
                {
                    output += eventArgs.Data + "\n";
                }
            };
        }

        string? error = null;
        if (args.CaptureError)
        {
            error = "";
            process.ErrorDataReceived += (_, eventArgs) =>
            {
                if (eventArgs.Data is not null)
                {
                    error += eventArgs.Data + "\n";
                }
            };
        }

        process.Start();
        if (args.CaptureOutput)
        {
            process.BeginOutputReadLine();
        }

        if (args.CaptureError)
        {
            process.BeginErrorReadLine();
        }

        await process.WaitForExitAsync(cancellationToken: cancellationToken);
        var exitCode = process.ExitCode;
        return (exitCode, output, error);
    }
}