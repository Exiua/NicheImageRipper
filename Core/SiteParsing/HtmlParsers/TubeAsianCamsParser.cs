using System.Diagnostics;
using System.Text.RegularExpressions;
using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.SiteParsing.VideoCapturers;
using Core.Utility;
using OpenQA.Selenium;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public partial class TubeAsianCamsParser : HtmlParser
{
    public TubeAsianCamsParser(WebDriver driver, Dictionary<string, string> requestHeaders,
                               FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders,
        filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for site and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//h2[@class='entry-title']").InnerText;
        var (capturer, b) = await ConfigureNetworkCapture<TubeAsianCamVideoCapturer>();
        await using var bidi = b;
        await WaitForElement("//iframe");
        var iframe = Driver.FindElement(By.XPath("//iframe"));
        Driver.SwitchTo().Frame(iframe);
        var startButton = Driver.FindElement(By.XPath("//div[@id='a']"));
        startButton.Click();
        string filename;
        var images = new List<StringImageLinkWrapper>();
        while (true)
        {
            var links = capturer.GetNewVideoLinks();
            if (links.Count == 0)
            {
                continue;
            }

            string url;
            var match = UrlParameterForEncodedPlaylistUrlRegex().Match(links[0]);
            if (match.Success)
            {
                url = match.Value;
                url = Uri.UnescapeDataString(url);
            }
            else
            {
                url = links[0];
            }
            
            filename = UrlUtility.GetUrlParameterValue(url, "t");
            var imageLink = new ImageLink(url, FilenameScheme, 0, filename: filename)
            {
                Referer = "https://jilliandescribecompany.com/"
            };
            images.Add(imageLink);
            break;
        }

        return RipInfo.FromUrlListWithFilenames(images, dirName, FilenameScheme, filenames: [filename]);
    }

    [GeneratedRegex("(?<=[?&]mu=)[^&]*")]
    private static partial Regex UrlParameterForEncodedPlaylistUrlRegex();
}