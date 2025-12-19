using Briefed.Core.Entities;
using Briefed.Core.Interfaces;
using Briefed.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartReader;
using Article = Briefed.Core.Entities.Article;

namespace Briefed.Infrastructure.Services;

public class ArticleService : IArticleService
{
    private readonly BriefedDbContext _context;
    private readonly ILogger<ArticleService> _logger;
    private readonly HttpClient _httpClient;

    public ArticleService(BriefedDbContext context, ILogger<ArticleService> logger)
    {
        _context = context;
        _logger = logger;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task<Article?> GetArticleByIdAsync(int articleId)
    {
        return await _context.Articles
            .Include(a => a.Feed)
            .Include(a => a.Summary)
            .FirstOrDefaultAsync(a => a.Id == articleId);
    }

    public async Task<IEnumerable<Article>> GetArticlesByFeedIdAsync(int feedId)
    {
        return await _context.Articles
            .Where(a => a.FeedId == feedId)
            .OrderByDescending(a => a.PublishedAt)
            .Take(50)
            .ToListAsync();
    }

    public async Task<IEnumerable<Article>> GetUserArticlesAsync(string userId, bool? isRead = null)
    {
        var query = _context.UserFeeds
            .Where(uf => uf.UserId == userId)
            .SelectMany(uf => uf.Feed.Articles)
            .AsQueryable();

        if (isRead.HasValue)
        {
            query = from article in query
                    join userArticle in _context.UserArticles
                        on new { UserId = userId, ArticleId = article.Id }
                        equals new { userArticle.UserId, userArticle.ArticleId }
                        into userArticleGroup
                    from ua in userArticleGroup.DefaultIfEmpty()
                    where ua == null && !isRead.Value || ua != null && ua.IsRead == isRead.Value
                    select article;
        }

        var articles = await query
            .Include(a => a.Feed)
            .Include(a => a.Summary)
            .OrderByDescending(a => a.PublishedAt)
            .AsNoTracking()
            .ToListAsync();

        // Strip HTML from descriptions after loading
        foreach (var article in articles)
        {
            if (!string.IsNullOrEmpty(article.Description))
            {
                var cleaned = article.Description;
                
                // Decode HTML entities first
                cleaned = System.Net.WebUtility.HtmlDecode(cleaned);
                
                // Remove all HTML tags (loop until no more tags found)
                string previous;
                do
                {
                    previous = cleaned;
                    cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"<[^>]*>", string.Empty);
                } while (previous != cleaned);
                
                // Clean up extra whitespace
                cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s+", " ");
                cleaned = cleaned.Trim();
                
                article.Description = cleaned;
            }
        }

        return articles;
    }

    public async Task<Article> CreateArticleAsync(Article article)
    {
        _context.Articles.Add(article);
        await _context.SaveChangesAsync();
        return article;
    }

    public async Task UpdateArticleAsync(Article article)
    {
        _context.Articles.Update(article);
        await _context.SaveChangesAsync();
    }

    public async Task MarkAsReadAsync(string userId, int articleId)
    {
        var userArticle = await _context.UserArticles
            .FirstOrDefaultAsync(ua => ua.UserId == userId && ua.ArticleId == articleId);

        if (userArticle == null)
        {
            userArticle = new UserArticle
            {
                UserId = userId,
                ArticleId = articleId,
                IsRead = true,
                ReadAt = DateTime.UtcNow
            };
            _context.UserArticles.Add(userArticle);
        }
        else if (!userArticle.IsRead)
        {
            userArticle.IsRead = true;
            userArticle.ReadAt = DateTime.UtcNow;
            _context.UserArticles.Update(userArticle);
        }

        await _context.SaveChangesAsync();
    }

    public async Task MarkAllAsReadForFeedAsync(string userId, int feedId)
    {
        var articleIds = await _context.Articles
            .Where(a => a.FeedId == feedId)
            .Select(a => a.Id)
            .ToListAsync();

        var existingUserArticles = await _context.UserArticles
            .Where(ua => ua.UserId == userId && articleIds.Contains(ua.ArticleId))
            .ToListAsync();

        var existingArticleIds = existingUserArticles.Select(ua => ua.ArticleId).ToHashSet();
        var now = DateTime.UtcNow;

        // Mark existing ones as read
        foreach (var userArticle in existingUserArticles)
        {
            if (!userArticle.IsRead)
            {
                userArticle.IsRead = true;
                userArticle.ReadAt = now;
            }
        }

        // Create new UserArticle entries for articles that don't have them yet
        var newUserArticles = articleIds
            .Where(id => !existingArticleIds.Contains(id))
            .Select(id => new UserArticle
            {
                UserId = userId,
                ArticleId = id,
                IsRead = true,
                ReadAt = now
            })
            .ToList();

        if (newUserArticles.Any())
        {
            _context.UserArticles.AddRange(newUserArticles);
        }

        await _context.SaveChangesAsync();
    }

    public async Task<string?> GetArticleContentAsync(string url)
    {
        try
        {
            var article = await Reader.ParseArticleAsync(url);
            
            if (article == null || string.IsNullOrWhiteSpace(article.Content))
            {
                _logger.LogWarning("Failed to extract content from {Url}", url);
                return null;
            }

            return article.Content; // Use HTML content instead of plain text
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting article content from {Url}", url);
            return null;
        }
    }

    public async Task<SavedArticle?> SaveArticleAsync(string userId, int articleId, string content)
    {
        var existing = await _context.SavedArticles
            .FirstOrDefaultAsync(sa => sa.UserId == userId && sa.ArticleId == articleId);

        if (existing != null)
        {
            return existing;
        }

        var savedArticle = new SavedArticle
        {
            UserId = userId,
            ArticleId = articleId,
            Content = content
        };

        _context.SavedArticles.Add(savedArticle);
        await _context.SaveChangesAsync();

        return savedArticle;
    }

    public async Task<IEnumerable<SavedArticle>> GetSavedArticlesAsync(string userId)
    {
        return await _context.SavedArticles
            .Where(sa => sa.UserId == userId)
            .Include(sa => sa.Article)
                .ThenInclude(a => a.Feed)
            .OrderByDescending(sa => sa.SavedAt)
            .ToListAsync();
    }

    public async Task<SavedArticle?> GetSavedArticleAsync(string userId, int articleId)
    {
        return await _context.SavedArticles
            .Include(sa => sa.Article)
                .ThenInclude(a => a.Feed)
            .FirstOrDefaultAsync(sa => sa.UserId == userId && sa.ArticleId == articleId);
    }

    public async Task DeleteSavedArticleAsync(int savedArticleId)
    {
        var savedArticle = await _context.SavedArticles.FindAsync(savedArticleId);
        if (savedArticle != null)
        {
            _context.SavedArticles.Remove(savedArticle);
            await _context.SaveChangesAsync();
        }
    }
}
