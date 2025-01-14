using Core;

namespace CoreTest;

public class NicheImageRipperTest
{
    public static TheoryData<string, string> Urls =>
        new()
        {
            {
                "https://www.pornhub.com/view_video.php?viewkey=ph6248078d3e901&index=1",
                "https://www.pornhub.com/view_video.php?viewkey=ph6248078d3e901"
            },
            {
                "https://www.pornhub.com/album/79074201?test=853004031",
                "https://www.pornhub.com/album/79074201"
            },
            {
                "https://www.pornhub.com/photo/853004031?test=dadwd21312",
                "https://www.pornhub.com/photo/853004031"
            },
            {
                "https://www.pornhub.com/gif/50749771?test=123123",
                "https://www.pornhub.com/gif/50749771"
            },
            {
                "https://danbooru.donmai.us/posts?tags=yuyu_%28yuyuworks%29&z=1",
                "https://danbooru.donmai.us/posts?tags=yuyu_%28yuyuworks%29"
            },
            {
                "https://gelbooru.com/index.php?page=post&s=list&tags=zanamaoria&pid=42",
                "https://gelbooru.com/index.php?page=post&s=list&tags=zanamaoria"
            },
            {
                "https://rule34.xxx/index.php?page=post&s=list&tags=blue_archive&pid=126",
                "https://rule34.xxx/index.php?page=post&s=list&tags=blue_archive"
            },
            {
                "https://yande.re/post?page=4&tags=niliu_chahui",
                "https://yande.re/post?tags=niliu_chahui"
            }
        };
    
    [Theory]
    [MemberData(nameof(Urls))]
    public void NormalizeUrlTest(string link, string expected)
    {
        var actual = NicheImageRipper.NormalizeUrl(link);
        Assert.Equal(expected, actual);
    }
}