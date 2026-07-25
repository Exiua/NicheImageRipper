using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;

namespace NicheImageRipper.Core.FileDownloading.FileDownloadStrategies;

public sealed class SeleniumImageDownloadStrategy : IFileDownloadStrategy
{
    public IEnumerable<LinkInfo> HandlesLinkInfo => [LinkInfo.SeleniumImage];

    public async Task<DownloadResult> DownloadAsync(FileLink link, string imagePath, DownloadContext context,
                                                    CancellationToken cancellationToken = default)
    {
        try
        {
            var imageData = GetImageViaSelenium(link.Url, context);
            await File.WriteAllBytesAsync(imagePath, imageData, cancellationToken);
            return DownloadResult.Success();
        }
        catch (Exception e)
        {
            context.Logger.Error(e, "Failed to download image");
            return DownloadResult.Failed("Failed to download image via Selenium");
        }
    }

    private static byte[] GetImageViaSelenium(string url, DownloadContext context)
    {
        var driver = context.WebDriver.Driver;
        driver.Url = url;
        var b64Img = (string?)driver.ExecuteScript("""
                                                   const img = document.getElementsByTagName("img")[0];
                                                   const canvas = document.createElement("canvas");
                                                   canvas.width = img.naturalWidth;
                                                   canvas.height = img.naturalHeight;
                                                   const ctx = canvas.getContext("2d");
                                                   ctx.drawImage(img, 0, 0);
                                                   const dataURL = canvas.toDataURL("image/png");
                                                   return dataURL.replace(/^data:image\/(png|jpg);base64,/, "");
                                                   """);
        return Convert.FromBase64String(b64Img!);
    }
}