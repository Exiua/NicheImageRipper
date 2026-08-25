using Sdk.DataStructures;

namespace Sdk.FileDownloading;

public interface IRequestHeaderModifier
{
    bool AppliesTo(string url, FileLink link, DownloadContext context);

    /// <returns>Map of header key → value to restore after the request (null value = remove the key).</returns>
    Task<IReadOnlyDictionary<string, string?>> ApplyAsync(Dictionary<string, string> requestHeaders,
                                                          string url, FileLink link, DownloadContext context,
                                                          CancellationToken cancellationToken);
}