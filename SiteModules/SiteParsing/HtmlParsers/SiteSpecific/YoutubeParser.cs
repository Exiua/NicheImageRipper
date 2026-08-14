using System.Diagnostics;
using System.Text;
using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing;
using NicheImageRipper.Core.Exceptions;
using NicheImageRipper.Core.SiteParsing.HtmlParsers;
using OpenQA.Selenium;
using HtmlAgilityPack;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.SiteParsing.HtmlParsers.SiteSpecific;
public class YoutubeParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "youtube";
    public static string[] SupportedUrls => ["https://www.youtube.com/"];

    public YoutubeParser(WebDriver driver, ApiClientManager apiClientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, apiClientManager, requestHeaders, IHtmlParser.GetFilenameScheme<YoutubeParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for site and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        // TODO: May be able to rectify this in PostProcess by renaming files after download
        if (FilenameScheme != FilenameScheme.Original)
        {
            Logger.Warning("YoutubeParser only supports Original filename scheme. Files will be saved with original filenames.");
        }

        var url = CurrentUrl.Split("/").Take(4).Join('/');
        CurrentUrl = url;
        var soup = await Soupify(xpath: "//h1[@class='dynamicTextViewModelH1']/span");
        var displayName = soup.SelectSingleNodeOrThrow("//h1[@class='dynamicTextViewModelH1']/span").InnerText;
        var username = soup.SelectSingleNodeOrThrow("//span[@class='yt-core-attributed-string yt-content-metadata-view-model__metadata-text yt-core-attributed-string--white-space-pre-wrap yt-core-attributed-string--link-inherit-color']").InnerText;
        var dirName = $"{displayName} ({username})";
        var args = new SubprocessArgs("yt-dlp").WithArgs("--flat-playlist", "--encoding", "utf-8", "--print", "\"%(title)s|%(id)s\"", CurrentUrl).EnableOutputCapture();
        var(exitCode, output, error) = await RunSubprocess(args);
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

    private static async Task<(int, string? , string? )> RunSubprocess(SubprocessArgs args, CancellationToken cancellationToken = default)
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

        await process.WaitForExitAsync();
        var exitCode = process.ExitCode;
        return (exitCode, output, error);
    }
}