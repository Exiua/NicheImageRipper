using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.SiteParsing;
using NicheImageRipper.SiteModules.Modules.Cdn;
using NicheImageRipper.SiteModules.Modules.IframeMediaDelivery;

namespace SiteModules.Tests;

/// <summary>
///     The registry used to scan only the Sdk assembly, so rules defined in site modules (and Core) were never
///     found and their links fell back to a plain HTTP download.
/// </summary>
public class SiteLinkRuleRegistryTest
{
    static SiteLinkRuleRegistryTest()
    {
        // Make sure the module assembly is loaded before the registry scans for rules
        _ = typeof(IframeMediaDeliveryLinkRule).Assembly;
    }

    [Fact]
    public void ModuleRulesAreFound()
    {
        Assert.IsType<IframeMediaDeliveryLinkRule>(
            SiteLinkRuleRegistry.FindMatch("https://iframe.mediadelivery.net/embed/1/abc"));
        Assert.IsType<SendvidLinkRule>(
            SiteLinkRuleRegistry.FindMatch("https://videos2.sendvid.com/aa/bb/cc/dd/ee/video.m3u8"));
    }

    [Fact]
    public void ModuleLinkInfoIsUsedWhenResolving()
    {
        var resolved = SiteLinkRuleRegistry
                      .FindMatch("https://iframe.mediadelivery.net/embed/1/abc{x}https://example.com/page")!
                      .Resolve("https://iframe.mediadelivery.net/embed/1/abc{x}https://example.com/page");

        Assert.Equal(IframeMediaDeliveryLinkInfo.IframeMedia, resolved.LinkInfo);
        Assert.Equal("iframe-media", ((LinkInfo)resolved.LinkInfo!).Name);
    }

    [Fact]
    public void UnrelatedUrlMatchesNothing()
    {
        Assert.Null(SiteLinkRuleRegistry.FindMatch("https://example.com/gallery/1.jpg"));
    }
}
