using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing;
using NicheImageRipper.Core.Exceptions;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.SiteParsing.HtmlParsers.SiteSpecific;

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

        var dirName = await GetChannelDisplayName(url, cancellationToken);

        var listArgs = new SubprocessArgs("yt-dlp")
                      .WithArgs("--flat-playlist", "--encoding", "utf-8", "--print", "\"%(title)s|%(id)s\"", url)
                      .EnableOutputCapture();
        var (exitCode, output, error) = await RunSubprocess(listArgs, cancellationToken: cancellationToken);
        if (exitCode != 0)
        {
            Logger.Error("yt-dlp failed with exit code {ExitCode}. Error: {Error}", exitCode, error);
            throw new RipperException("yt-dlp subprocess failed");
        }

        var images = output!.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line =>
        {
            var split = line.LastIndexOf('|');
            var title = line[..split];
            var id = line[(split + 1)..];
            var videoUrl = $"https://www.youtube.com/watch?v={id}";
            var fileLink = FileLink.WithFilename(videoUrl, $"{title}.webm", FilenameScheme, cleanFilename: true);
            return fileLink;
        }).ToStringImageLinkWrapperList();

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }

    private async Task<string> GetChannelDisplayName(string url, CancellationToken cancellationToken)
    {
        var metadataArgs = new SubprocessArgs("yt-dlp")
                          .WithArgs("--flat-playlist", "--playlist-items", "0", "--dump-single-json", url)
                          .EnableOutputCapture()
                          .EnableErrorCapture();
        var (exitCode, output, error) = await RunSubprocess(metadataArgs, cancellationToken: cancellationToken);
        if (exitCode != 0 || string.IsNullOrWhiteSpace(output))
        {
            Logger.Warning("Failed to fetch channel metadata via yt-dlp (exit {ExitCode}): {Error}", exitCode, error);
            return "Unknown Channel";
        }

        var json = JsonSerializer.Deserialize<JsonNode>(output);
        var displayName = json?["channel"]?.GetValue<string>() ?? json?["uploader"]?.GetValue<string>();
        var username = json?["uploader_id"]?.GetValue<string>() ?? json?["channel_id"]?.GetValue<string>();

        return displayName is null
            ? "Unknown Channel"
            : username is null
                ? displayName
                : $"{displayName} ({username})";
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