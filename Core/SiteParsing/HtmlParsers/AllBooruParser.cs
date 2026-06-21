using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers;

public class AllBooruParser : BooruParser, IHtmlParser
{
    public static string ParserName => "booru";

    public AllBooruParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<AllBooruParser>(filenameScheme))
    {
    }

    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var tags = ExtractTagsFromUrl(GivenUrl);
        Logger.Debug("Parsing all boorus with tags: {Tags}", tags);
        var boorus = Enum.GetValues<Booru>();
        var images = new List<StringImageLinkWrapper>();
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
                return (StringImageLinkWrapper)u;
            });
            images.AddRange(urls);
        }
        
        var tagTitle = tags.Remove("+").Remove("tags=");
        tagTitle = Uri.UnescapeDataString(tagTitle);
        var dirName = $"[Booru] {tagTitle}";
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}