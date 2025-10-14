using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class AllBooruParser : BooruParser
{
    public AllBooruParser(WebDriver driver, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, filenameScheme)
    {
    }

    public override async Task<RipInfo> Parse()
    {
        var tags = ExtractTagsFromUrl(GivenUrl);
        Log.Debug("Parsing all boorus with tags: {Tags}", tags);
        var boorus = Enum.GetValues<Booru>();
        var images = new List<StringImageLinkWrapper>();
        foreach (var booru in boorus)
        {
            //Log.Debug("Parsing {Booru}", booru);
            var metadata = booru.GetMetadata();
            var referer = metadata.BaseUrl.Split("/")[..3].Join("/") + "/";
            var posts = await BooruParse(booru, tags);
            Log.Information("Found {NumUrls} images on {Booru}", posts.NumUrls, booru);
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