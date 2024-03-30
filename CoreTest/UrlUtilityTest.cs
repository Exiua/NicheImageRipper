using Core;

namespace CoreTest;

public class UrlUtilityTest
{
    public static IEnumerable<object[]> GDriveLinks =>
        new List<object[]>
        {
            new object[]
            {
                "https://drive.google.com/file/d/1q4qAaea0kALCnvcodTLqXTL2VtIP120V/view",
                "https://drive.google.com/file/d/1q4qAaea0kALCnvcodTLqXTL2VtIP120V"
            },
            new object[]
            {
                "Prefixhttps://drive.google.com/open?id=1rc9iUp9ZHxr2KZhyuDiMtE1vFf9HcboaHey",
                "https://drive.google.com/open?id=1rc9iUp9ZHxr2KZhyuDiMtE1vFf9Hcboa"
            },
            new object[]
            {
                "https://drive.google.com/drive/folders/1xYdcixnVbRGVX7CIfBOC1YIGKCeZ6a9u?usp=sharing(One",
                "https://drive.google.com/drive/folders/1xYdcixnVbRGVX7CIfBOC1YIGKCeZ6a9u?usp=sharing"
            },
            new object[]
            {
                @"https://drive.google.com/drive/u/0/folders/15M2sy76TjqQRYxd7X0EFjqjZRZdh_wQQ|\xb7t\xc7t\xb3\xfc\xc1p\xad.X\xd5T\xcf\xa4\xc2\xa8\xbc\x88\xc9",
                "https://drive.google.com/drive/u/0/folders/15M2sy76TjqQRYxd7X0EFjqjZRZdh_wQQ"
            },
            new object[]
            {
                "https://drive.google.com/file/d/14IgS-WYXgtGb3XSbxf2HlPxpsp6_bXI4/view?usp=share_linkSorry",
                "https://drive.google.com/file/d/14IgS-WYXgtGb3XSbxf2HlPxpsp6_bXI4/view?usp=share_link"
            },
            new object[]
            {
                "https://drive.google.com/file/d/1q4qAaea0kALCnvc",
                ""
            },
        };

    public static IEnumerable<object[]> MegaLinks =>
        new List<object[]>
        {
            new object[]
            {
                "https://mega.nz/folder/s7dmnSIS#RmFL5zxGBHwsUjHJgEocbwSorry",
                "https://mega.nz/folder/s7dmnSIS#RmFL5zxGBHwsUjHJgEocbw"
            },
            new object[]
            {
                "https://mega.nz/folder/0nt1WZCY#s-uB3iozoQUSoYGU",
                ""
            },
            new object[]
            {
                "https://mega.nz/file/ti9gkSab#daz8ahh0y0DcTrHIRKpxxxabxjFWH_lklU9scNaVvb8The",
                "https://mega.nz/file/ti9gkSab#daz8ahh0y0DcTrHIRKpxxxabxjFWH_lklU9scNaVvb8"
            },
            new object[]
            {
                "link:https://mega.nz/folder/InkHmaSS#ugwe1m_6qH1OXmGbS7qqLA",
                "https://mega.nz/folder/InkHmaSS#ugwe1m_6qH1OXmGbS7qqLA"
            }
        };
    
    [Theory]
    [MemberData(nameof(GDriveLinks))]
    public void GDriveLinkParseTest(string link, string expected)
    {
        var actual = UrlUtility.ExtractUrl(link);
        Assert.Equal(expected, actual);    
    }
    
    [Theory]
    [MemberData(nameof(MegaLinks))]
    public void MegaLinkParseTest(string link, string expected)
    {
        var actual = UrlUtility.ExtractUrl(link);
        Assert.Equal(expected, actual);    
    }
}