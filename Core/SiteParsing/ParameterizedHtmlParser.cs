using Core.DataStructures;
using Core.Enums;
using Core.Managers;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing;

public abstract class ParameterizedHtmlParser : HtmlParser
{
    protected ParameterizedHtmlParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    public override Task<RipInfo> Parse()
    {
        return Parse("");
    }

    public abstract Task<RipInfo> Parse(string url);
}