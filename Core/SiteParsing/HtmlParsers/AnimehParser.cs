using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using OpenQA.Selenium;
using WebDriver = Core.History.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class AnimehParser : HtmlParser
{
    public AnimehParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for animeh.to and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        string dirName;
        List<StringImageLinkWrapper> images;
        if(CurrentUrl.Contains("/hchapter/"))
        {
            CurrentUrl = CurrentUrl.Split("?")[0] + "?tab=reading";
            var soup = await Soupify();
            dirName = soup.SelectSingleNode("//h1[@class='main-night container']").InnerText
                              .Split("Manga")[1]
                              .Trim();
            var metadata = soup.SelectSingleNode("//div[@class='col-md-8 col-sm-12']");
            var pageCountRaw = metadata.SelectNodes("./div")
                                       .First(div => div.InnerText.Contains("Page"))
                                       .InnerText.Split(" ")[1];
            var pageCount = int.Parse(pageCountRaw);
            var imageUrl = soup.SelectSingleNode("//div[@id='pictureViewer']")
                               .SelectSingleNode(".//img")
                               .GetSrc();
            var urlParts = imageUrl.Split("/");
            imageUrl = "/".Join(urlParts[..^1]);
            var extension = urlParts[^1].Split(".")[1];
            images = [];
            for (var i = 1; i <= pageCount; i++)
            {
                var image = $"{imageUrl}/{i}.{extension}";
                images.Add(image);
            }
            
            return new RipInfo(images, dirName, FilenameScheme, generate: true, numUrls: pageCount);
        }

        if (CurrentUrl.Contains("/hepisode/"))
        {
            // var highestQualityButton = Driver.FindElement(By.XPath("(//div[@class='source-btn-group']/a)[2]"));
            // highestQualityButton.Click();
            var soup = await Soupify();
            dirName = soup.SelectSingleNode("//h1[@class='main-night']").InnerText
                          .Split("Hentai")[1]
                          .Trim();
            var playButton = Driver.FindElement(By.XPath("//button[@class='btn btn-play']"));
            playButton.Click();
            await WaitForElement("//iframe[@id='episode-frame']", timeout: -1);
            var iframe = Driver.FindElement(By.XPath("//iframe[@id='episode-frame']"));
            Driver.SwitchTo().Frame(iframe);
            await WaitForElement("//video/source", timeout: -1);
            var videoSource = Driver.FindElement(By.XPath("//video/source")).GetSrc();
            images = [videoSource];
        }
        else // Assume ASMR
        {
            var soup = await Soupify();
            dirName = soup.SelectSingleNode("//h1[@class='main-night']").InnerText
                          .Split(" ")[2..]
                          .Join(" ");
            images = [];
            var tracks =
                Driver.FindElements(By.XPath("//*[@id='__layout']/div/div[3]/main/div[1]/div/div[1]/div[1]/div[2]/a"));
            foreach (var track in tracks)
            {
                await Task.Delay(100);
                track.Click();
                var trackSource = Driver.FindElement(By.XPath("//audio")).GetSrc();
                images.Add(trackSource);
            }

            var thumbnails = Driver.FindElement(By.XPath("//div[@class='d-flex image-board mb image-board-asmr']"))
                                   .FindElements(By.XPath("./div[@class='d-flex flex-column']"));
            foreach (var thumbnail in thumbnails)
            {
                thumbnail.Click();
                var thumbnailSource = Driver.FindElement(By.XPath("//div[@class='vgs__container']//img"))
                                            .GetSrc();
                images.Add(thumbnailSource);
                var closeButton = Driver.FindElement(By.XPath("//button[@class='btn btn-danger vgs__close']"));
                closeButton.Click();
                await Task.Delay(500);
            }
        }

        return new RipInfo(images, dirName, FilenameScheme);
    }
}