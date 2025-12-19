using System.Xml.Linq;
using Briefed.Core.Interfaces;

namespace Briefed.Infrastructure.Services;

public class OpmlImportService
{
    public List<(string Title, string FeedUrl)> ParseOpml(Stream opmlStream)
    {
        var feeds = new List<(string Title, string FeedUrl)>();

        try
        {
            var doc = XDocument.Load(opmlStream);
            var outlines = doc.Descendants("outline")
                .Where(o => o.Attribute("xmlUrl") != null);

            foreach (var outline in outlines)
            {
                var title = outline.Attribute("title")?.Value 
                    ?? outline.Attribute("text")?.Value 
                    ?? "Untitled Feed";
                var feedUrl = outline.Attribute("xmlUrl")?.Value;

                if (!string.IsNullOrEmpty(feedUrl))
                {
                    feeds.Add((title, feedUrl));
                }
            }
        }
        catch (Exception)
        {
            throw new InvalidOperationException("Failed to parse OPML file. Please ensure it's a valid OPML export.");
        }

        return feeds;
    }
}
