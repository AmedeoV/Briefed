using Briefed.Core.Entities;

namespace Briefed.Core.Interfaces;

public interface IRssParserService
{
    Task<(IEnumerable<Article> Articles, string? FeedTitle, string? FeedDescription, string? SiteUrl)> ParseFeedAsync(string feedUrl);
}
