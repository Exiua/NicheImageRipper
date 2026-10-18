using NicheImageRipper.SiteModules.Modules.Booru;

namespace SiteModules.Tests;

public class BooruInputExpanderTest
{
    private readonly BooruInputExpander _expander = new();

    [Fact]
    public void TagsAndSearchUrlsSplitIntoSeparateSearches()
    {
        var urls = _expander.TryExpand("booru cute tall https://example.booru.com/post?tags=cat+animal funny");

        Assert.Equal([
            "https://booru.com/post?tags=cute+tall",
            "https://booru.com/post?tags=cat+animal",
            "https://booru.com/post?tags=funny",
        ], urls);
    }

    [Fact]
    public void ExtraWhitespaceAndKeywordCaseDontMatter()
    {
        Assert.Equal(["https://booru.com/post?tags=a+b"], _expander.TryExpand("  BOORU   a \t b  "));
    }

    [Fact]
    public void UrlWithoutTagsIsSkipped()
    {
        Assert.Equal(["https://booru.com/post?tags=x"], _expander.TryExpand("booru https://example.com/gallery/1 x"));
    }

    [Fact]
    public void MissingTagsQueuesNothing()
    {
        Assert.Empty(_expander.TryExpand("booru")!);
    }

    [Theory]
    [InlineData("https://example.com/gallery/1")]
    [InlineData("boorutag")]
    [InlineData("")]
    public void OtherInputIsNotHandled(string input)
    {
        Assert.Null(_expander.TryExpand(input));
    }
}
