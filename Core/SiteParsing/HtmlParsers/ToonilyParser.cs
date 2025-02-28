using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using OpenQA.Selenium;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class ToonilyParser : HtmlParser
{
    public ToonilyParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for toonily.me and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var btn = Driver.TryFindElement(By.XPath("//div[@id='show-more-chapters']"));
        if (btn is not null)
        {
            ScrollElementIntoView(btn);
            await Task.Delay(250);
            btn.Click();
            await Task.Delay(1000);
        }
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//div[@class='name box']")
                            .SelectSingleNode(".//h1")
                            .InnerText;
        var chapterList = soup.SelectSingleNode("//ul[@id='chapter-list']")
                                .SelectNodes(".//li");
        var chapters = chapterList.Select(chapter => $"https://toonily.me{chapter.SelectSingleNode(".//a").GetHref()}");
        var images = new List<StringImageLinkWrapper>();
        foreach (var chapter in chapters.Reverse())
        {
            soup = await Soupify(chapter, lazyLoadArgs: new LazyLoadArgs {ScrollBy = true, Increment = 5000});
            var imageList = soup.SelectSingleNode("//div[@id='chapter-images']")
                                .SelectNodes(".//img");
            images.AddRange(imageList.Select(img => (StringImageLinkWrapper)img.GetSrc()));
        }
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
