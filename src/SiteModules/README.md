# SiteModules

The built-in site parsers: one folder per supported site under `Modules/`. Each parser knows how to find the images
and videos on its site and returns them for [Core](../Core) to download.

## Adding A Site

Create `Modules/<Site>/<Site>Parser.cs`:

```csharp
public class ExampleParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "example";
    public static string[] SupportedUrls => ["https://www.example.com/"];

    public ExampleParser(WebDriver driver, Dictionary<string, string> requestHeaders,
                         FilenameScheme filenameScheme = FilenameScheme.Original)
        : base(driver, requestHeaders, IHtmlParser.GetFilenameScheme<ExampleParser>(filenameScheme))
    {
    }

    public override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow("//h1").InnerText;
        var images = soup.SelectNodesOrThrow("//div[@class='gallery']//img")
                         .Select(img => img.GetSrc()).ToStringFileLinks();
        return await RipInfo.FromUrlListAsync(images, dirName, FilenameScheme);
    }
}
```

Parsers are discovered by reflection, so nothing else needs registering. `SupportedUrls` must be the normalized base
URL (scheme, host, trailing slash). Implement the optional interfaces in [Sdk](../../lib/Sdk/src/Sdk)'s `SiteParsing/`
for extra behavior, such as `IMultiSiteHtmlParser` for mirror domains or `IUpdatableHtmlParser` for galleries that
can be re-ripped for new files.

Try a parser against a real URL with [SiteModulesParserTest](../../test/SiteModulesParserTest), and add unit tests
to [SiteModules.Tests](../../test/SiteModules.Tests).
