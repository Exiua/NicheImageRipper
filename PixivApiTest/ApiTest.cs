using Common.ExtensionMethods;
using PixivApi;

namespace PixivApiTest;

public class ApiTest
{
    private const string RefreshToken = "";
    
    [Fact]
    public async Task GetUgoiraMetadata()
    {
        var client = new PixivApiClient();
        _ = await client.Auth(RefreshToken);
        var metadata = await client.UgoiraMetadata("73453365");
        Assert.False(metadata.Error);
        Assert.NotNull(metadata.Body);
    }

    [Fact]
    public async Task GetUserIllust()
    {
        const string userId = "186899";
        var client = new PixivApiClient();
        _ = await client.Auth(RefreshToken);
        var illusts = await client.UserIllusts(userId);
        Assert.False(illusts.Illusts.Count == 0);
        var seen = illusts.Illusts.Select(illust => illust.Id).ToHashSet();
        var illusts2 = await client.UserIllusts(userId, offset: 30);
        Assert.False(illusts2.Illusts.Count == 0);
        Assert.True(illusts2.Illusts.All(illust => !seen.Contains(illust.Id)));
    }

    [Fact]
    public async Task DownloadUserIllust()
    {
        const string userId = "186899";
        var client = new PixivApiClient();
        _ = await client.Auth(RefreshToken);
        var illusts = await client.UserIllusts(userId);
        Assert.False(illusts.Illusts.Count == 0);
        var firstIllust = illusts.Illusts[0];
        var imageUrl = firstIllust.MetaPages[0].ImageUrls.Large.ToOriginalUrl();
        var imageData = await client.Download(imageUrl, path: "./", name: $"test_{userId}.jpg");
        Assert.True(imageData);
        var imageUrl2 = firstIllust.MetaPages[1].ImageUrls.Large.ToOriginalUrl();
        var imageData2 = await client.Download(imageUrl2, path: "./", name: $"test_{userId}_2.jpg");
        Assert.True(imageData2);
    }

    [Fact]
    public async Task GetIllustDetail()
    {
        const string illustId = "135732445";
        var client = new PixivApiClient();
        _ = await client.Auth(RefreshToken);
        var illustDetail = await client.IllustDetail(illustId);
        Assert.Equal(illustId, illustDetail.Id.ToString());
    }

    [Fact]
    public async Task GetUserUgoira()
    {
        const string userId = "14395837";
        var client = new PixivApiClient();
        _ = await client.Auth(RefreshToken);
        var illusts = await client.UserIllusts(userId);
        Assert.False(illusts.Illusts.Count == 0);
        var ugoiraIllust = illusts.Illusts.FirstOrDefault(illust => illust.Type == "ugoira");
        Assert.NotNull(ugoiraIllust);
        var metadata = await client.UgoiraMetadata(ugoiraIllust.Id.ToString());
        Assert.False(metadata.Error);
        Assert.True(metadata.Body is not null || metadata.BodyReal is not null);
    }
    
    [Fact]
    public async Task GetUserUgoira2()
    {
        const string userId = "70470754";
        var client = new PixivApiClient();
        _ = await client.Auth(RefreshToken);
        var illusts = await client.UserIllusts(userId);
        Assert.False(illusts.Illusts.Count == 0);
        var ugoiraIllust = illusts.Illusts.FirstOrDefault(illust => illust.Type == "ugoira");
        Assert.NotNull(ugoiraIllust);
        var metadata = await client.UgoiraMetadata(ugoiraIllust.Id.ToString());
        Assert.False(metadata.Error);
        Assert.True(metadata.Body is not null || metadata.BodyReal is not null);
    }
}