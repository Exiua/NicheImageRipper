using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Serilog;
using WebDriver = Core.History.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class AllBooruParser : HtmlParser
{
    public AllBooruParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    public override async Task<RipInfo> Parse()
    {
        var tags = BooruRegex().Match(GivenUrl).Groups[1].Value;
        var boorus = new[] { Booru.Danbooru, Booru.Gelbooru, Booru.Rule34, Booru.Yandere };
        var images = new List<StringImageLinkWrapper>();
        foreach (var booru in boorus)
        {
            Log.Debug("Parsing {Booru}", booru);
            var metadata = booru.GetMetadata();
            var referer = metadata.BaseUrl.Split("/")[..3].Join("/") + "/";
            var posts = await BooruParse(booru, tags);
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
        return new RipInfo(images, dirName, FilenameScheme);
    }
}