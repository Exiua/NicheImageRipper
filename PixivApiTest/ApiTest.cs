using PixivApi;

namespace PixivApiTest;

public class ApiTest
{
    [Fact]
    public async Task GetUgoiraMetadata()
    {
        var client = new PixivApiClient();
        var authResponse = await client.Auth("");
        var metadata = await client.UgoiraMetadata("73453365");
        Assert.False(metadata.Error);
        Assert.NotNull(metadata.Body);
    }
}