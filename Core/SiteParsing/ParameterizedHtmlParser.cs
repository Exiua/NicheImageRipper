using Core.DataStructures;
using Core.Enums;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing;

public abstract class ParameterizedHtmlParser : HtmlParser
{
    protected ParameterizedHtmlParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    public override Task<RipInfo> Parse()
    {
        return Parse("");
    }

    public abstract Task<RipInfo> Parse(string url);
}