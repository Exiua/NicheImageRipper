using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Exceptions;
using NicheImageRipper.Core.Utility;

namespace NicheImageRipper.Core.FileDownloading.FileDownloadStrategies;

using static DownloadLogging;

public sealed class GenericHttpDownloadStrategy : IFileDownloadStrategy
{
    private const int RetryCount = 4;
    private const int MillisecondsInSecond = 1000;
    private const int MinimumFileSize = 1024; // 1KB

    /// <inheritdoc />
    public IEnumerable<LinkInfo> HandlesLinkInfo => [LinkInfo.None, LinkInfo.GoFile];

    /// <inheritdoc />
    public async Task<DownloadResult> DownloadAsync(FileLink link, string imagePath, DownloadContext context,
                                                    CancellationToken cancellationToken = default)
    {
        if (imagePath[^1] == '/')
        {
            imagePath = imagePath[..^1];
        }

        var result = DownloadResult.Failed();
        for (var attempt = 0; attempt < RetryCount; attempt++)
        {
            result = await AttemptDownload(link, imagePath, false, context, cancellationToken);
            if (result.Outcome is DownloadOutcome.Success or DownloadOutcome.SkipNotAFailure)
            {
                break;
            }
        }

        // Retries exhausted without success — Retry is not a valid terminal state for the caller.
        if (result.Outcome == DownloadOutcome.Retry)
        {
            result = DownloadResult.Failed(result.Reason ?? "Retries exhausted");
        }

        if (result.Outcome is not (DownloadOutcome.Success or DownloadOutcome.SkipNotAFailure))
        {
            return result;
        }

        if (result.Outcome == DownloadOutcome.Success && Path.GetExtension(imagePath) == "")
        {
            await FixMissingExtension(link, imagePath, context, cancellationToken);
        }

        return result;
    }

    private async Task FixMissingExtension(FileLink link, string imagePath, DownloadContext context,
                                           CancellationToken cancellationToken)
    {
        context.Logger.Debug("Finding correct extension for file: {ImagePath}", imagePath);
        var extension = FileUtility.GetCorrectExtension(imagePath);
        await RenameFile(imagePath, imagePath + extension, context, cancellationToken);
        var filename = Path.GetFileName(imagePath);
        var newFilename = filename + extension;
        context.Logger.Debug("Renamed file {OldFilename} to {NewFilename}", filename, newFilename);
        link.Filename = newFilename;
    }

    private async Task RenameFile(string src, string dst, DownloadContext context, CancellationToken cancellationToken)
    {
        if (!File.Exists(dst))
        {
            File.Move(src, dst);
            return;
        }

        var srcHash = await FileUtility.GetFileHash(src, cancellationToken);
        var dstHash = await FileUtility.GetFileHash(dst, cancellationToken);
        if (srcHash.SequenceEqual(dstHash))
        {
            context.Logger.Information("File already exists and is same, deleting src...");
            File.Delete(src);
        }
        else
        {
            var ext = Path.GetExtension(dst);
            var filename = Path.GetFileNameWithoutExtension(dst);
            var directory = Path.GetDirectoryName(dst)!;
            var newFilename = $"{filename} ({DateTime.Now:yyyy-MM-dd HH-mm-ss}){ext}";
            File.Move(src, Path.Combine(directory, newFilename));
            context.Logger.Information("File already exists but is different, renaming src...");
        }
    }

    private async Task<DownloadResult> AttemptDownload(FileLink link, string imagePath, bool generatingManually,
                                                        DownloadContext context, CancellationToken cancellationToken)
    {
        if (link.IsInvalid)
        {
            if (context.SiteName == "e-hentai")
            {
                // Used to force generation of unparsed links (partial-parsing approach — may fire multiple times per rip)
                throw new UrlExpiredException(context.SiteName);
            }

            throw new RipperException("Invalid ImageLink found for non-EHentai site");
        }

        var url = link.Url;
        await Task.Delay((int)(context.SleepTime * MillisecondsInSecond), cancellationToken);

        var headerModifier = context.HeaderModifiers.FirstOrDefault(m => m.AppliesTo(url, link, context));
        IReadOnlyDictionary<string, string?>? restoreMap = null;
        if (headerModifier is not null)
        {
            restoreMap = await headerModifier.ApplyAsync(context.RequestHeaders, url, link, context, cancellationToken);
        }

        context.Logger.Debug("Request Headers: {@RequestHeaders}", context.RequestHeaders);
        var resumeFrom = 0L;
        while (true)
        {
            HttpResponseMessage response;
            try
            {
                using var request = context.RequestHeaders.ToRequest(HttpMethod.Get, url);
                if (resumeFrom > 0)
                {
                    request.Headers.Range = new RangeHeaderValue(resumeFrom, null);
                    context.Logger.Information("Resuming download from byte {Offset}", resumeFrom);
                }

                response = await context.Session.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
            }
            catch (HttpRequestException e) when (e.InnerException is InvalidOperationException or SocketException)
            {
                context.Logger.Error("Unable to establish a connection to {Url}", url);
                return DownloadResult.Failed("Unable to establish a connection");
            }

            if (!response.IsSuccessStatusCode)
            {
                return await HandleUnsuccessfulStatusCode(response, url, link, generatingManually, context,
                    cancellationToken);
            }

            DownloadStatus writeStatus;
            try
            {
                writeStatus = await WriteToFile(response, imagePath, resumeFrom, context, cancellationToken);
            }
            catch (DownloadTimeoutException e)
            {
                if (e.DownloadedBytesCount == resumeFrom)
                {
                    context.Logger.Warning("No progress made during download, aborting...");
                    throw;
                }

                resumeFrom += e.DownloadedBytesCount;
                continue;
            }
            catch (Exception e)
            {
                context.Logger.Debug("Exception during file write: {Exception}", e);
                throw;
            }

            switch (writeStatus)
            {
                case DownloadStatus.None:
                case DownloadStatus.Ok:
                    break;
                case DownloadStatus.ConnectionReset:
                    return DownloadResult.Failed("Connection reset");
                case DownloadStatus.Failed:
                    LogFailedUrl(url);
                    return DownloadResult.Failed("Write failed");
                default:
                    throw new ArgumentOutOfRangeException($"Enum value not handled: {writeStatus}");
            }

            // NOTE: matches original — only restored on the success path, not on any earlier `return` above.
            if (restoreMap is not null)
            {
                RestoreHeaders(context.RequestHeaders, restoreMap);
            }

            var validator = context.PostDownloadValidators.FirstOrDefault(v => v.AppliesTo(link, context));
            return validator is not null
                ? await validator.ValidateAsync(imagePath, link, context, cancellationToken)
                : DownloadResult.Success();
        }
    }

    private static void RestoreHeaders(Dictionary<string, string> requestHeaders,
                                       IReadOnlyDictionary<string, string?> restoreMap)
    {
        foreach (var (key, value) in restoreMap)
        {
            if (value is null)
            {
                requestHeaders.Remove(key);
            }
            else
            {
                requestHeaders[key] = value;
            }
        }
    }

    private async Task<DownloadStatus> WriteToFile(HttpResponseMessage response, string path, long resumeFrom,
                                                    DownloadContext context, CancellationToken cancellationToken)
    {
        var expandedFilePath = path.StartsWith('~')
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[1..])
            : path;

        var idleTimeout = TimeSpan.FromSeconds(30);
        var savePath = Path.GetFullPath(expandedFilePath);
        try
        {
            return await BufferedWrite(response, savePath, idleTimeout, resumeFrom, context, cancellationToken);
        }
        catch (HttpRequestException)
        {
            context.Logger.Warning("Connection Reset, Retrying...");
            await Task.Delay(1000, cancellationToken);
            return DownloadStatus.ConnectionReset;
        }
    }

    private async Task<DownloadStatus> BufferedWrite(HttpResponseMessage response, string savePath,
                                                      TimeSpan idleTimeout, long resumeFrom, DownloadContext context,
                                                      CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = new FileStream(savePath,
            resumeFrom > 0 ? FileMode.Append : FileMode.Create,
            FileAccess.Write,
            FileShare.None);
        var buffer = new byte[4096];
        var totalSize = 0L;
        var lastActivity = DateTime.UtcNow;

        try
        {
            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                totalSize += bytesRead;
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);

                if (DateTime.UtcNow - lastActivity > idleTimeout)
                {
                    context.Logger.Warning("Download timed out due to inactivity.");
                    throw new DownloadTimeoutException(totalSize, "No data received for too long.");
                }

                lastActivity = DateTime.UtcNow;
            }
        }
        catch (IOException e)
        {
            if (e.Message.StartsWith("The response ended prematurely"))
            {
                context.Logger.Warning("Download response ended prematurely.");
                throw new DownloadTimeoutException(totalSize, e.Message, e);
            }

            if (e.Message.StartsWith("Received an unexpected EOF or 0 bytes from the transport stream"))
            {
                context.Logger.Warning("Received unexpected EOF from transport stream.");
                throw new DownloadTimeoutException(totalSize, e.Message, e);
            }

            context.Logger.Error("An IO error occured: {SavePath} - Reason: {Reason}", savePath, e.Message);
            return DownloadStatus.Failed;
        }

        // Generic floor for every site — see EHentaiMinimumSizeValidator for the e-hentai-specific
        // post-success re-check, which catches "successfully downloaded, but it's an HTML error page" cases
        // too large to trip this coarse threshold.
        if (totalSize < MinimumFileSize)
        {
            context.Logger.Warning("Downloaded file is very small: {SavePath} ({Size} bytes)", savePath, totalSize);
            return DownloadStatus.Failed;
        }

        return DownloadStatus.Ok;
    }

    private static readonly HashSet<HttpStatusCode> KnownStatusCodes =
    [
        HttpStatusCode.NotFound,
        HttpStatusCode.Unauthorized,
        HttpStatusCode.Forbidden,
        HttpStatusCode.BadGateway,
        HttpStatusCode.InternalServerError,
    ];

    private async Task<DownloadResult> HandleUnsuccessfulStatusCode(HttpResponseMessage response, string url,
        FileLink link, bool generatingManually, DownloadContext context, CancellationToken cancellationToken)
    {
        context.Logger.Warning("<Response {ResponseStatusCode}>", response.StatusCode);
        await Task.Delay(500, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            LogFailedUrl(url);
            if (generatingManually)
            {
                throw new WrongExtensionException();
            }
        }

        var handler = context.ErrorHandlers.FirstOrDefault(h => h.AppliesTo(response.StatusCode, url, link, context));
        if (handler is not null)
        {
            return await handler.HandleAsync(response, url, link, generatingManually, context, cancellationToken);
        }

        if (!KnownStatusCodes.Contains(response.StatusCode))
        {
            context.Logger.Warning("Unhandled status code: {ResponseStatusCode}", response.StatusCode);
        }

        return DownloadResult.Failed($"Status code: {response.StatusCode}");
    }
}