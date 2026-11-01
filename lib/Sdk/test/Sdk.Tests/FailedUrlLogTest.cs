using System.Net;
using NicheImageRipper.Sdk.Exceptions;
using NicheImageRipper.Sdk.FileDownloading;
using NicheImageRipper.Sdk.Utility;

namespace NicheImageRipper.Sdk.Tests;

[Collection(AppDataCollection.Name)]
public sealed class FailedUrlLogTest : IDisposable
{
    private readonly string _originalDirectory = AppData.Directory;
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"nir-failedurls-{Guid.NewGuid():N}");

    public FailedUrlLogTest()
    {
        AppData.Directory = _directory;
    }

    public void Dispose()
    {
        AppData.Directory = _originalDirectory;
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void RecordedEntriesReadBackInOrder()
    {
        var parse = FailedUrlEntry.Create(FailureStage.Parse, FailureKind.Unsupported, "SomeParser",
            "https://site.test/gallery/1", reason: "This page type is not supported");
        var download = FailedUrlEntry.Create(FailureStage.Download, FailureKind.NotFound, "GenericHttpDownloadStrategy",
            "https://cdn.site.test/1.jpg", "https://site.test/gallery/2", "Status code: NotFound");

        FailedUrlLog.Record(parse);
        FailedUrlLog.RecordAll([download]);

        Assert.Equal([parse, download], FailedUrlLog.ReadAll());
    }

    [Fact]
    public void EachEntryIsOneLineWithReadableEnums()
    {
        FailedUrlLog.Record(FailedUrlEntry.Create(FailureStage.Download, FailureKind.NotFound, "Strategy",
            "https://cdn.site.test/1.jpg", reason: "line one\nline two"));

        var lines = File.ReadAllLines(AppData.GetPath(FailedUrlLog.FileName));

        var line = Assert.Single(lines);
        Assert.Contains("\"Stage\":\"Download\"", line);
        Assert.Contains("\"Kind\":\"NotFound\"", line);
    }

    [Fact]
    public void ReadAllSkipsLinesThatAreNotEntries()
    {
        var entry = FailedUrlEntry.Create(FailureStage.Rip, FailureKind.Other, "FileRipper", "https://site.test/a");
        FailedUrlLog.Record(entry);
        AppData.AppendAllText(FailedUrlLog.FileName, "not json\n");

        Assert.Equal([entry], FailedUrlLog.ReadAll());
    }

    [Fact]
    public void ReadAllIsEmptyWithoutAFile()
    {
        Assert.Empty(FailedUrlLog.ReadAll());
    }

    [Fact]
    public void Base64UrlsAreShortened()
    {
        var url = "data:image/png;base64," + new string('A', 5000);

        var entry = FailedUrlEntry.Create(FailureStage.Download, FailureKind.Other, "Base64DownloadStrategy", url);

        Assert.True(entry.Url.Length < 200);
    }

    public static TheoryData<Exception, FailureKind> ClassifiedExceptions => new()
    {
        { new FileNotFoundAtUrlException("gone"), FailureKind.NotFound },
        { new FileNotFoundException("gone"), FailureKind.NotFound },
        { new HttpRequestException("gone", null, HttpStatusCode.NotFound), FailureKind.NotFound },
        { new HttpRequestException("gone", null, HttpStatusCode.Gone), FailureKind.NotFound },
        { new HttpRequestException("busy", null, HttpStatusCode.TooManyRequests), FailureKind.Other },
        { new FeatureNotSupportedException("Parser", "new page type"), FailureKind.Unsupported },
        { new ElementNotFoundException("//div"), FailureKind.Unsupported },
        { new OpenQA.Selenium.NoSuchElementException("missing"), FailureKind.Unsupported },
        { new RipperException("wrapped", new ElementNotFoundException("//div")), FailureKind.Unsupported },
        { new InvalidOperationException("unexpected"), FailureKind.Other },
    };

    [Theory]
    [MemberData(nameof(ClassifiedExceptions))]
    public void ClassifyMapsExceptionsToKinds(Exception exception, FailureKind expected)
    {
        Assert.Equal(expected, FailedUrlLog.Classify(exception));
    }

    [Fact]
    public void DownloadResultsCarryTheirFailureKind()
    {
        Assert.Equal(FailureKind.NotFound, DownloadResult.NotFound().FailureKind);
        Assert.Equal(DownloadOutcome.Failed, DownloadResult.NotFound().Outcome);
        Assert.Null(DownloadResult.Failed("reason").FailureKind);
        Assert.Equal(FailureKind.NotFound,
            DownloadResult.SkipNotAFailure("gone", FailureKind.NotFound).FailureKind);
    }
}
