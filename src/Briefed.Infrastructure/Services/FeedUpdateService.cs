using Briefed.Core.Entities;
using Briefed.Core.Interfaces;
using Briefed.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Briefed.Infrastructure.Services;

public class FeedUpdateService
{
    private readonly BriefedDbContext _context;
    private readonly IRssParserService _rssParser;
    private readonly ILogger<FeedUpdateService> _logger;

    public FeedUpdateService(BriefedDbContext context, IRssParserService rssParser, ILogger<FeedUpdateService> logger)
    {
        _context = context;
        _rssParser = rssParser;
        _logger = logger;
    }

    public async Task UpdateAllFeedsAsync()
    {
        var feeds = await _context.Feeds
            .Where(f => f.IsActive)
            .ToListAsync();

        _logger.LogInformation("Starting update for {FeedCount} feeds", feeds.Count);

        var successCount = 0;
        var failCount = 0;

        foreach (var feed in feeds)
        {
            try
            {
                _logger.LogInformation("Processing feed: {FeedTitle} (ID: {FeedId}) - {FeedUrl}", feed.Title, feed.Id, feed.Url);
                await UpdateFeedAsync(feed);
                successCount++;
                _logger.LogInformation("✓ Successfully updated feed: {FeedTitle}", feed.Title);
            }
            catch (Exception ex)
            {
                failCount++;
                _logger.LogError(ex, "✗ Failed to update feed {FeedId}: {FeedTitle} ({FeedUrl}) - Error: {ErrorMessage}", 
                    feed.Id, feed.Title, feed.Url, ex.Message);
            }
        }

        _logger.LogInformation("Completed feed updates: {SuccessCount} succeeded, {FailCount} failed out of {TotalCount} total", 
            successCount, failCount, feeds.Count);
    }

    public async Task UpdateFeedAsync(Feed feed)
    {
        try
        {
            _logger.LogInformation("Starting update for feed: {FeedTitle} ({FeedUrl})", feed.Title, feed.Url);
            
            var (articles, feedTitle, feedDescription, siteUrl) = await _rssParser.ParseFeedAsync(feed.Url);

            _logger.LogInformation("Fetched {ArticleCount} articles from {FeedTitle}", articles.Count(), feed.Title);

            // Get existing article URLs for this feed
            var existingUrls = await _context.Articles
                .Where(a => a.FeedId == feed.Id)
                .Select(a => a.Url)
                .ToListAsync();

            // Get deleted article URLs (tombstones) to prevent re-fetching
            var deletedUrls = await _context.DeletedArticles
                .Select(a => a.Url)
                .ToListAsync();

            _logger.LogInformation("Feed {FeedTitle} has {ExistingCount} existing articles, {DeletedCount} deleted articles (tombstones)", 
                feed.Title, existingUrls.Count, deletedUrls.Count);

            // Only accept articles from the last 14 days and not in deleted list
            var cutoffDate = DateTime.UtcNow.AddDays(-14);
            var newArticles = articles
                .Where(a => !existingUrls.Contains(a.Url) 
                         && !deletedUrls.Contains(a.Url)
                         && a.PublishedAt >= cutoffDate)
                .ToList();

            if (newArticles.Any())
            {
                foreach (var article in newArticles)
                {
                    article.FeedId = feed.Id;
                }

                _context.Articles.AddRange(newArticles);
                _logger.LogInformation("Adding {Count} new articles for feed {FeedTitle} (filtered {Filtered} old/deleted articles)", 
                    newArticles.Count, feed.Title, articles.Count() - newArticles.Count - existingUrls.Count);
            }
            else
            {
                _logger.LogInformation("No new articles found for feed {FeedTitle}", feed.Title);
            }

            feed.LastFetchedAt = DateTime.UtcNow;
            
            // Update SiteUrl and FaviconUrl if not set
            if (string.IsNullOrEmpty(feed.SiteUrl) && !string.IsNullOrEmpty(siteUrl))
            {
                feed.SiteUrl = siteUrl;
                try
                {
                    var uri = new Uri(siteUrl!);
                    feed.FaviconUrl = $"{uri.Scheme}://{uri.Host}/favicon.ico";
                }
                catch { }
            }
            
            _context.Feeds.Update(feed);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message?.Contains("IX_Articles_Url") == true)
            {
                // Handle duplicate URLs across different feeds - try individual inserts
                _logger.LogWarning("Batch insert failed due to duplicate URLs for feed {FeedTitle}. Trying individual inserts...", feed.Title);
                
                // Clear the change tracker
                _context.ChangeTracker.Clear();
                
                // Reload the feed from database so EF tracks it properly
                var feedFromDb = await _context.Feeds.FindAsync(feed.Id);
                if (feedFromDb == null)
                {
                    _logger.LogError("Feed {FeedId} not found in database after ChangeTracker.Clear()", feed.Id);
                    return;
                }
                
                // Add articles one by one, checking for duplicates
                var addedCount = 0;
                foreach (var article in newArticles)
                {
                    try
                    {
                        // Check if URL exists globally
                        var exists = await _context.Articles.AnyAsync(a => a.Url == article.Url);
                        if (!exists)
                        {
                            _context.Articles.Add(article);
                            await _context.SaveChangesAsync();
                            addedCount++;
                        }
                    }
                    catch
                    {
                        // Skip this article if it still fails
                        _context.ChangeTracker.Clear();
                        // Reload feed again after clearing
                        feedFromDb = await _context.Feeds.FindAsync(feed.Id);
                    }
                }
                
                _logger.LogInformation("Added {Count} articles individually for feed {FeedTitle}", addedCount, feed.Title);
                
                // Update the feed metadata using the tracked entity
                if (feedFromDb != null)
                {
                    feedFromDb.LastFetchedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating feed {FeedId}: {FeedUrl}", feed.Id, feed.Url);
            throw;
        }
    }

    public async Task UpdateFeedFaviconsAsync()
    {
        var feeds = await _context.Feeds
            .Where(f => f.IsActive && string.IsNullOrEmpty(f.FaviconUrl) && !string.IsNullOrEmpty(f.SiteUrl))
            .ToListAsync();

        foreach (var feed in feeds)
        {
            try
            {
                var uri = new Uri(feed.SiteUrl);
                feed.FaviconUrl = $"{uri.Scheme}://{uri.Host}/favicon.ico";
            }
            catch
            {
                // Skip if URL is invalid
            }
        }

        if (feeds.Any())
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Updated {Count} feeds with favicon URLs", feeds.Count);
        }
    }
    
    public async Task ResyncFeedArticleDatesAsync(int feedId)
    {
        var feed = await _context.Feeds.FindAsync(feedId);
        if (feed == null)
        {
            throw new InvalidOperationException($"Feed {feedId} not found");
        }
        
        _logger.LogInformation("Re-syncing article dates for feed: {FeedTitle} ({FeedUrl})", feed.Title, feed.Url);
        
        var (articles, _, _, _) = await _rssParser.ParseFeedAsync(feed.Url);
        
        // Get existing articles for this feed
        var existingArticles = await _context.Articles
            .Where(a => a.FeedId == feedId)
            .ToListAsync();
        
        var updatedCount = 0;
        foreach (var existingArticle in existingArticles)
        {
            var feedArticle = articles.FirstOrDefault(a => a.Url == existingArticle.Url);
            if (feedArticle != null && feedArticle.PublishedAt != existingArticle.PublishedAt)
            {
                _logger.LogInformation("Updating date for article: {Title} from {OldDate} to {NewDate}", 
                    existingArticle.Title, existingArticle.PublishedAt, feedArticle.PublishedAt);
                existingArticle.PublishedAt = feedArticle.PublishedAt;
                updatedCount++;
            }
        }
        
        if (updatedCount > 0)
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Updated {Count} article dates for feed {FeedTitle}", updatedCount, feed.Title);
        }
        else
        {
            _logger.LogInformation("No article dates needed updating for feed {FeedTitle}", feed.Title);
        }
    }
}
