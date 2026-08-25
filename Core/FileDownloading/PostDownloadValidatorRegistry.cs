using NicheImageRipper.Core.DataStructures;
using Sdk.DataStructures;
using Sdk.FileDownloading;

namespace NicheImageRipper.Core.FileDownloading;

public sealed class PostDownloadValidatorRegistry
{
    private readonly List<IPostDownloadValidator> _validators;
    public PostDownloadValidatorRegistry(IEnumerable<IPostDownloadValidator> validators) => _validators = validators.ToList();
    public IPostDownloadValidator? FindMatch(FileLink link, DownloadContext context) =>
        _validators.FirstOrDefault(v => v.AppliesTo(link, context));
}