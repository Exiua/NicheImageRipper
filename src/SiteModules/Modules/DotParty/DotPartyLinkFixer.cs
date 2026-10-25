using HtmlAgilityPack;

namespace NicheImageRipper.SiteModules.Modules.DotParty;

/// <summary>
/// Fixes links that have been split by the site's HTML — so far, only mega.nz links have been observed
/// to be split along the hash (#) character if present in the link.
/// </summary>
internal static class DotPartyLinkFixer
{
    public static void FixLinks(HtmlNode soup)
    {
        // Select all anchor tags under this root
        var anchors = soup.SelectNodes(".//a");
        if (anchors is null)
        {
            return;
        }

        // Track text nodes to remove
        var nodesToRemove = new List<HtmlNode>();
        HtmlNode? anchorToFix = null;
        foreach (var anchor in anchors)
        {
            var parent = anchor.ParentNode;
            foreach (var node in parent.ChildNodes)
            {
                switch (node.NodeType)
                {
                    case HtmlNodeType.Element:
                    {
                        // Check only <a> nodes
                        if (node.Name.Equals("a", StringComparison.OrdinalIgnoreCase))
                        {
                            var href = node.GetAttributeValue("href", "");
                            if (href.Contains("https://mega.nz/"))
                            {
                                // Mark anchor as needing merge
                                anchorToFix = node;
                            }
                        }

                        break;
                    }

                    case HtmlNodeType.Text:
                    {
                        if (anchorToFix != null)
                        {
                            var text = node.InnerText;
                            // The hash extension must begin with "#"
                            if (!text.StartsWith('#'))
                            {
                                // Anchor was not split, skip
                                anchorToFix = null;
                                continue;
                            }

                            // Merge text into anchor
                            var newHref = anchorToFix.InnerText + text;
                            anchorToFix.InnerHtml = HtmlDocument.HtmlEncode(newHref); // display text
                            anchorToFix.SetAttributeValue("href", newHref); // link URL
                            // Schedule removal of trailing text node
                            nodesToRemove.Add(node);
                            anchorToFix = null;
                        }

                        break;
                    }

                    case HtmlNodeType.Document:
                    case HtmlNodeType.Comment:
                        anchorToFix = null; // This probably shouldn't happen, but just in case
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        // Now remove all text nodes that were merged
        foreach (var n in nodesToRemove)
        {
            n.Remove();
        }
    }
}