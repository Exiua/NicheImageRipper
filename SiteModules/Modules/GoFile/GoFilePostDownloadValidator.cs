using System.Text.Json;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.FileDownloading;
using NicheImageRipper.Core.FileDownloading.FileDownloadStrategies;
using NicheImageRipper.Core.Utility;
using OpenQA.Selenium;

namespace NicheImageRipper.SiteModules.Modules.GoFile;

public sealed class GoFilePostDownloadValidator : IPostDownloadValidator
{
    private const int RetryCount = 4;

    public bool AppliesTo(FileLink link, DownloadContext context) => link.LinkInfo == LinkInfo.GoFile;

    public async Task<DownloadResult> ValidateAsync(string filePath, FileLink link, DownloadContext context,
                                                    CancellationToken cancellationToken)
    {
        var ext = FileUtility.GetCorrectExtension(filePath);
        if (ext != ".html")
        {
            return DownloadResult.Success();
        }

        context.Logger.Warning("GoFile download failed, trying again...");
        await AssociateGoFileCookies(link.Url, context, cancellationToken);
        return DownloadResult.Retry();
    }

    private async Task AssociateGoFileCookies(string url, DownloadContext context, CancellationToken cancellationToken)
    {
        context.Logger.Debug("Associating GoFile cookies");
        var siteLoginStatus = context.WebDriver.SiteLoginStatus;
        try
        {
            if (!siteLoginStatus.GetValueOrDefault("gofile", false))
            {
                context.Logger.Debug("Logging into GoFile");
                siteLoginStatus["gofile"] = await GoFileLogin(context, cancellationToken);
            }

            var driver = context.WebDriver.Driver;
            driver.Url = url;
            context.Logger.Debug("Loading {CurrentUrl}", driver.Url);
            driver.Refresh();
            await Task.Delay(5000, cancellationToken);
        }
        catch (WebDriverException)
        {
            context.Logger.Warning("WebDriver unreachable, resetting...");
            context.WebDriver.RegenerateDriver();
        }
    }

    private async Task<bool> GoFileLogin(DownloadContext context, CancellationToken cancellationToken)
    {
        var driver = context.WebDriver.Driver;
        var origUrl = driver.Url;
        var config = ConfigAccess.Config.Custom.GetValueOrDefault("gofile").Deserialize<CustomConfig.GoFileConfig>();
        driver.Url = config.LoginLink;
        await Task.Delay(10000, cancellationToken);

        for (var i = 0; i < RetryCount; i++)
        {
            await Task.Delay(2500, cancellationToken);
            if (driver.Url == "https://gofile.io/myProfile")
            {
                context.Logger.Debug("Logged in to GoFile");
                break;
            }

            if (i == RetryCount - 1)
            {
                context.Logger.Warning("Failed to login to GoFile: {CurrentUrl}", driver.Url);
                #if DEBUG
                driver.TakeDebugScreenshot("gofile.png");
                #endif
            }
        }

        driver.Url = origUrl;
        return true;
    }
}