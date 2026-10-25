using System.Text.Json;
using NicheImageRipper.SiteModules.Modules.MangaDex;

namespace SiteModules.Tests;

public class MangaDexLanguageTest
{
    [Theory]
    [InlineData(null, "en")]
    [InlineData("""{ "Language": "ja" }""", "ja")]
    [InlineData("""{ "language": " es-la " }""", "es-la")]
    [InlineData("""{ "Language": "" }""", "en")]
    [InlineData("""{ "Other": 1 }""", "en")]
    [InlineData("""{ "Language": 5 }""", "en")]
    [InlineData("""["not", "an", "object"]""", "en")]
    public void LanguageComesFromSiteConfig(string? customJson, string expected)
    {
        JsonElement? custom = customJson is null ? null : JsonDocument.Parse(customJson).RootElement.Clone();

        Assert.Equal(expected, MangaDexConfig.From(custom).Language);
    }

    private static readonly Dictionary<string, string> Title = new() { ["ja-ro"] = "Romaji Title" };

    private static readonly Dictionary<string, string>[] AltTitles =
    [
        new() { ["en"] = "English Title" },
        new() { ["ja"] = "日本語タイトル" },
    ];

    [Fact]
    public void TitleUsesConfiguredLanguageFirst()
    {
        Assert.Equal("日本語タイトル", MangaDexParser.ChooseTitle(Title, AltTitles, "ja"));
    }

    [Fact]
    public void TitleFallsBackToEnglish()
    {
        Assert.Equal("English Title", MangaDexParser.ChooseTitle(Title, AltTitles, "fr"));
    }

    [Fact]
    public void MainTitleWinsOverAlternativeInSameLanguage()
    {
        var title = new Dictionary<string, string> { ["en"] = "Main English" };

        Assert.Equal("Main English", MangaDexParser.ChooseTitle(title, AltTitles, "en"));
    }

    [Fact]
    public void TitleFallsBackToAnyTitle()
    {
        Assert.Equal("Romaji Title", MangaDexParser.ChooseTitle(Title, [], "fr"));
    }
}
