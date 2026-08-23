using NicheImageRipper.Core.SiteParsing;
using NicheImageRipper.Core.Utility;

namespace NicheImageRipper.SiteModules.Modules.DotParty;

internal static class DotPartyExternalLinkExtractor
{
    public static Dictionary<string, List<string>> ExtractPossibleExternalUrls(List<string> possibleUrls)
    {
        var externalLinks = ExternalLinkExtractor.CreateExternalLinkDict();
        foreach (var site in externalLinks.Keys)
        {
            foreach (var text in possibleUrls)
            {
                if (!text.Contains(site))
                {
                    continue;
                }

                var parts = text.Split();
                foreach (var part in parts)
                {
                    if (!part.Contains(site))
                    {
                        continue;
                    }

                    var link = UrlUtility.ExtractUrl(part);
                    if (link != "")
                    {
                        externalLinks[site].Add(link + '\n');
                    }
                }
            }
        }

        return externalLinks;
    }

    public static string ReplacePlusWithSpaceInFilename(string filename)
    {
        var newFilename = "";
        var lastChar = '\0';
        foreach (var c in filename)
        {
            switch (c)
            {
                case '+':
                    if (lastChar == '+')
                    {
                        newFilename += '+';
                    }

                    break;
                default:
                    if (lastChar == '+')
                    {
                        newFilename += ' ';
                    }

                    newFilename += c;
                    break;
            }

            lastChar = c;
        }

        return newFilename;
    }
}