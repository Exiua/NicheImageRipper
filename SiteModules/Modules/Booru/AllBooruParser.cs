using NicheImageRipper.Sdk.Common.ExtensionMethods;
using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.Booru;

public class AllBooruParser : BooruParser, IHtmlParser
{
    public static string ParserName => "booru";
    public static string[] SupportedUrls => ["https://booru.com/"];
    protected override bool RequiresNavigation => false;

    public AllBooruParser(WebDriver driver,  Dictionary<string, string> requestHeaders,
                          FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, 
        requestHeaders, IHtmlParser.GetFilenameScheme<AllBooruParser>(filenameScheme))
    {
    }

    public override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var tags = ExtractTagsFromUrl(GivenUrl);
        Logger.Debug("Parsing all boorus with tags: {Tags}", tags);
        var boorus = Enum.GetValues<Sdk.Enums.Booru>();
        var images = new List<StringFileLinkWrapper>();
        foreach (var booru in boorus)
        {
            //Logger.Debug("Parsing {Booru}", booru);
            var metadata = booru.GetMetadata();
            var referer = metadata.BaseUrl.Split("/")[..3].Join("/") + "/";
            var posts = await BooruParse(booru, tags, cancellationToken);
            Logger.Information("Found {NumUrls} images on {Booru}", posts.NumUrls, booru);
            var urls = posts.Urls.Select(u =>
            {
                u.Referer = referer;
                return (StringFileLinkWrapper)u;
            });
            images.AddRange(urls);
        }

        var tagTitle = tags.Remove("+").Remove("tags=");
        tagTitle = Uri.UnescapeDataString(tagTitle);
        var dirName = $"[Booru] {tagTitle}";
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}