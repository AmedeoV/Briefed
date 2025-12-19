using Briefed.Core.Entities;
using Briefed.Core.Interfaces;
using Briefed.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Briefed.Infrastructure.Services;

public class FeedService : IFeedService
{
    private readonly BriefedDbContext _context;
    private readonly IRssParserService _rssParser;
    private readonly ILogger<FeedService> _logger;

    public FeedService(BriefedDbContext context, IRssParserService rssParser, ILogger<FeedService> logger)
    {
        _context = context;
        _rssParser = rssParser;
        _logger = logger;
    }

    public async Task<Feed?> GetFeedByIdAsync(int feedId)
    {
        return await _context.Feeds
            .Include(f => f.Articles)
            .FirstOrDefaultAsync(f => f.Id == feedId);
    }

    public async Task<Feed?> GetFeedByUrlAsync(string url)
    {
        return await _context.Feeds.FirstOrDefaultAsync(f => f.Url == url);
    }

    public async Task<IEnumerable<Feed>> GetAllFeedsAsync()
    {
        return await _context.Feeds
            .Where(f => f.IsActive)
            .OrderBy(f => f.Title)
            .ToListAsync();
    }

    public async Task<IEnumerable<Feed>> GetUserFeedsAsync(string userId)
    {
        return await _context.UserFeeds
            .Where(uf => uf.UserId == userId)
            .Include(uf => uf.Feed)
            .Select(uf => uf.Feed)
            .OrderBy(f => f.Title)
            .ToListAsync();
    }

    public async Task<Feed> CreateFeedAsync(Feed feed)
    {
        var existing = await GetFeedByUrlAsync(feed.Url);
        if (existing != null)
        {
            return existing;
        }

        try
        {
            var (articles, feedTitle, feedDescription, siteUrl) = await _rssParser.ParseFeedAsync(feed.Url);
            
            feed.Title = feedTitle ?? feed.Title;
            feed.Description = feedDescription ?? feed.Description;
            feed.SiteUrl = siteUrl ?? feed.SiteUrl;
            feed.LastFetchedAt = DateTime.UtcNow;
            
            // Try to get favicon
            if (!string.IsNullOrEmpty(feed.SiteUrl))
            {
                feed.FaviconUrl = GetFaviconUrl(feed.SiteUrl);
            }

            _context.Feeds.Add(feed);
            await _context.SaveChangesAsync();

            // Add all articles in a batch, checking for duplicates by URL within this feed only
            var addedCount = 0;
            var skippedCount = 0;
            var articlesToAdd = new List<Article>();
            
            foreach (var article in articles)
            {
                article.FeedId = feed.Id;
                
                // Check if article URL already exists for this specific feed
                var existingArticle = await _context.Articles
                    .FirstOrDefaultAsync(a => a.Url == article.Url && a.FeedId == feed.Id);
                
                if (existingArticle == null)
                {
                    articlesToAdd.Add(article);
                    addedCount++;
                }
                else
                {
                    skippedCount++;
                }
            }
            
            if (articlesToAdd.Any())
            {
                _context.Articles.AddRange(articlesToAdd);
                await _context.SaveChangesAsync();
            }
            
            _logger.LogInformation("Feed {FeedUrl}: Added {AddedCount} articles, skipped {SkippedCount} duplicates", 
                feed.Url, addedCount, skippedCount);

            return feed;
        }
        catch (Exception ex)
        {
            var errorMessage = ex.InnerException?.Message ?? ex.Message;
            _logger.LogError(ex, "Failed to create feed {FeedUrl}: {Error}", feed.Url, errorMessage);
            throw new InvalidOperationException($"Unable to add feed: {errorMessage}", ex);
        }
    }

    public async Task UpdateFeedAsync(Feed feed)
    {
        _context.Feeds.Update(feed);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteFeedAsync(int feedId)
    {
        var feed = await _context.Feeds.FindAsync(feedId);
        if (feed != null)
        {
            _context.Feeds.Remove(feed);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> SubscribeUserToFeedAsync(string userId, int feedId)
    {
        var existing = await _context.UserFeeds
            .FirstOrDefaultAsync(uf => uf.UserId == userId && uf.FeedId == feedId);

        if (existing != null)
        {
            return false;
        }

        var userFeed = new UserFeed
        {
            UserId = userId,
            FeedId = feedId
        };

        _context.UserFeeds.Add(userFeed);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UnsubscribeUserFromFeedAsync(string userId, int feedId)
    {
        var userFeed = await _context.UserFeeds
            .FirstOrDefaultAsync(uf => uf.UserId == userId && uf.FeedId == feedId);

        if (userFeed == null)
        {
            return false;
        }

        _context.UserFeeds.Remove(userFeed);
        await _context.SaveChangesAsync();
        return true;
    }

    private static string GetFaviconUrl(string siteUrl)
    {
        try
        {
            var uri = new Uri(siteUrl);
            return $"{uri.Scheme}://{uri.Host}/favicon.ico";
        }
        catch
        {
            return string.Empty;
        }
    }
}
