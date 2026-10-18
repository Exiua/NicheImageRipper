using NicheImageRipper.Core.Utility;

namespace NicheImageRipper.Core.Tests;

public class UrlListFileTest
{
    [Fact]
    public void ValidListIsRead()
    {
        const string json = """
                            [
                              "https://example.com/gallery/1",
                              "  http://example.org/post?id=2  ",
                            ]
                            """;

        Assert.True(UrlListFile.TryParse(json, out var urls, out var error));
        Assert.Equal("", error);
        Assert.Equal(["https://example.com/gallery/1", "http://example.org/post?id=2"], urls);
    }

    [Theory]
    [InlineData("not json", "Not valid JSON")]
    [InlineData("""{ "urls": ["https://example.com"] }""", "Expected a JSON array")]
    [InlineData("""["https://example.com", 5]""", "Item 2 isn't a string")]
    [InlineData("""["https://example.com", "example.org/no-scheme"]""", "Item 2 isn't an http(s) URL")]
    [InlineData("""["ftp://example.com/file"]""", "Item 1 isn't an http(s) URL")]
    [InlineData("""[""]""", "Item 1 isn't an http(s) URL")]
    [InlineData("[]", "The list is empty")]
    public void InvalidListIsRejected(string json, string expectedError)
    {
        Assert.False(UrlListFile.TryParse(json, out var urls, out var error));
        Assert.StartsWith(expectedError, error);
        Assert.Empty(urls);
    }

    [Fact]
    public void MissingFileIsRejected()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");

        Assert.False(UrlListFile.TryRead(path, out _, out var error));
        Assert.StartsWith("Couldn't read the file", error);
    }
}
