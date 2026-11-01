using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NicheImageRipper.Sdk.Exceptions;
using NicheImageRipper.Sdk.Utility;
using OpenQA.Selenium;
using Serilog;

namespace NicheImageRipper.Sdk.FileDownloading;

/// <summary>
///     The one record of URLs a parser or downloader couldn't handle: <see cref="FileName"/> in the data folder, one
///     JSON <see cref="FailedUrlEntry"/> per line, appended to across runs.
/// </summary>
/// <remarks>
///     The ripper records parse and download failures itself, once a gallery's retries are over. Call
///     <see cref="Record"/> directly only for failures it can't see, such as an item a parser skips over.
/// </remarks>
public static class FailedUrlLog
{
    public const string FileName = "failed_urls.jsonl";

    private static readonly Lock WriteLock = new();

    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static void Record(FailedUrlEntry entry) => RecordAll([entry]);

    /// <summary>Appends the entries. A write failure is logged rather than thrown, so it never fails a rip.</summary>
    public static void RecordAll(IEnumerable<FailedUrlEntry> entries)
    {
        var lines = new StringBuilder();
        foreach (var entry in entries)
        {
            lines.AppendLine(JsonSerializer.Serialize(entry, Options));
        }

        if (lines.Length == 0)
        {
            return;
        }

        try
        {
            lock (WriteLock)
            {
                AppData.AppendAllText(FileName, lines.ToString());
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.Warning("Couldn't write to {FileName}: {Reason}", FileName, e.Message);
        }
    }

    /// <summary>Reads every entry, skipping lines that aren't valid entries.</summary>
    public static List<FailedUrlEntry> ReadAll()
    {
        if (!AppData.Exists(FileName))
        {
            return [];
        }

        var entries = new List<FailedUrlEntry>();
        foreach (var line in File.ReadLines(AppData.GetPath(FileName)))
        {
            try
            {
                if (JsonSerializer.Deserialize<FailedUrlEntry>(line, Options) is { } entry)
                {
                    entries.Add(entry);
                }
            }
            catch (JsonException)
            {
            }
        }

        return entries;
    }

    /// <summary>Works out a <see cref="FailureKind"/> from the exception a parser or downloader threw.</summary>
    public static FailureKind Classify(Exception exception) => exception switch
    {
        FileNotFoundAtUrlException or FileNotFoundException => FailureKind.NotFound,
        HttpRequestException { StatusCode: HttpStatusCode.NotFound or HttpStatusCode.Gone } => FailureKind.NotFound,
        FeatureNotSupportedException or ElementNotFoundException or AttributeNotFoundException
            or ParameterizedParserNotFound or NoSuchElementException => FailureKind.Unsupported,
        { InnerException: { } inner } => Classify(inner),
        _ => FailureKind.Other,
    };
}
